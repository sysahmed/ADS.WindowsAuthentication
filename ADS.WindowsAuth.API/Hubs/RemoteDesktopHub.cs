using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;
using Microsoft.AspNetCore.SignalR;

namespace ADS.WindowsAuth.API.Hubs;

/// <summary>
/// SignalR Hub за Remote Desktop функционалност
/// </summary>
public class RemoteDesktopHub : Hub
{
    private readonly IRemoteDesktopSessionService _sessionService;
    private readonly ILoggerService _logger;

    public RemoteDesktopHub(
        IRemoteDesktopSessionService sessionService,
        ILoggerService logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    #region Host Methods

    /// <summary>
    /// Регистрира host машина за remote desktop сесия
    /// </summary>
    /// <param name="sessionId">ID на сесията</param>
    /// <param name="autoApprove">Автоматично одобряване на заявки за контрол</param>
    public async Task RegisterHost(string sessionId, bool autoApprove = false)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(sessionId);
            if (session == null)
            {
                await Clients.Caller.SendAsync("Error", "Сесията не съществува");
                return;
            }

            await _sessionService.RegisterHostAsync(sessionId, Context.ConnectionId, autoApprove);
            await Clients.Caller.SendAsync("HostRegistered", sessionId);

            _logger.LogInfo($"Host регистриран: Session={sessionId}, Connection={Context.ConnectionId}, AutoApprove={autoApprove}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при регистрация на host: {ex.Message}", ex);
            await Clients.Caller.SendAsync("Error", ex.Message);
        }
    }

    /// <summary>
    /// Изпраща screen frame към viewer (по session ID – host не трябва да знае viewerId)
    /// </summary>
    public async Task SendScreenFrameBySession(string sessionId, byte[] frameData)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(sessionId);
            if (session == null || string.IsNullOrEmpty(session.ViewerConnectionId))
                return;

            await Clients.Client(session.ViewerConnectionId).SendAsync("ReceiveFrame", frameData);
            // Обновяваме активността при всеки frame – сесията не трябва да изтича докато има стрийм
            await _sessionService.UpdateActivityAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при изпращане на frame: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Изпраща screen frame към viewer (legacy – по viewer connection ID)
    /// </summary>
    public async Task SendScreenFrame(string viewerId, byte[] frameData)
    {
        try
        {
            await Clients.Client(viewerId).SendAsync("ReceiveFrame", frameData);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при изпращане на frame: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Потвърждава получаване и изпълнение на input command
    /// </summary>
    public async Task AcknowledgeInput(string sessionId, string inputType)
    {
        try
        {
            await _sessionService.UpdateActivityAsync(sessionId);
            _logger.LogInfo($"Input acknowledged: Session={sessionId}, Type={inputType}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при acknowledge: {ex.Message}", ex);
        }
    }

    #endregion

    #region Viewer Methods

    /// <summary>
    /// Заявява достъп до host машина
    /// </summary>
    public async Task RequestControl(string sessionId, string userId)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(sessionId);
            if (session == null)
            {
                await Clients.Caller.SendAsync("ControlDenied", "Сесията не съществува");
                return;
            }

            if (string.IsNullOrEmpty(session.HostConnectionId))
            {
                await Clients.Caller.SendAsync("ControlDenied", "Host не е свързан");
                return;
            }

            await _sessionService.RegisterViewerAsync(sessionId, Context.ConnectionId, userId);

            if (session.AutoApprove)
            {
                await _sessionService.AuthorizeControlAsync(sessionId);
                await Clients.Caller.SendAsync("ControlGranted", sessionId);
                _logger.LogInfo($"Control auto-одобрен: Session={sessionId}, User={userId}");
            }
            else
            {
                await Clients.Client(session.HostConnectionId)
                    .SendAsync("ControlRequested", userId, Context.ConnectionId);
                _logger.LogInfo($"Control заявен: Session={sessionId}, User={userId}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при заявка за контрол: {ex.Message}", ex);
            await Clients.Caller.SendAsync("Error", ex.Message);
        }
    }

    /// <summary>
    /// Одобрява заявка за контрол (извиква се от host)
    /// </summary>
    public async Task ApproveControl(string sessionId)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(sessionId);
            if (session == null) return;

            await _sessionService.AuthorizeControlAsync(sessionId);

            if (!string.IsNullOrEmpty(session.ViewerConnectionId))
            {
                await Clients.Client(session.ViewerConnectionId)
                    .SendAsync("ControlGranted", sessionId);
            }

            _logger.LogInfo($"Control одобрен: Session={sessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при одобрение: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Отказва заявка за контрол (извиква се от host)
    /// </summary>
    public async Task DenyControl(string sessionId, string reason = "Отказан от host")
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(sessionId);
            if (session == null) return;

            await _sessionService.DenyControlAsync(sessionId);

            if (!string.IsNullOrEmpty(session.ViewerConnectionId))
            {
                await Clients.Client(session.ViewerConnectionId)
                    .SendAsync("ControlDenied", reason);
            }

            _logger.LogInfo($"Control отказан: Session={sessionId}, Reason={reason}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при отказ: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Проверява дали viewer-ът има право да изпраща команди.
    /// AutoApprove=true → само host да е свързан е достатъчно.
    /// </summary>
    private static bool CanControl(RemoteDesktopSession session)
        => session.ControlEnabled || session.AutoApprove;

    /// <summary>
    /// Изпраща mouse movement към host
    /// </summary>
    public async Task SendMouseMove(string sessionId, int x, int y)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(sessionId);
            if (session == null || !CanControl(session)) return;

            if (!string.IsNullOrEmpty(session.HostConnectionId))
            {
                await Clients.Client(session.HostConnectionId)
                    .SendAsync("ExecuteMouseMove", x, y);

                await _sessionService.UpdateActivityAsync(sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при mouse move: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Изпраща mouse click към host
    /// </summary>
    public async Task SendMouseClick(string sessionId, string button)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(sessionId);
            if (session == null || !CanControl(session)) return;

            if (!string.IsNullOrEmpty(session.HostConnectionId))
            {
                await Clients.Client(session.HostConnectionId)
                    .SendAsync("ExecuteMouseClick", button);

                await _sessionService.UpdateActivityAsync(sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при mouse click: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Viewer изпраща clipboard текст към host (Ctrl+V от браузъра)
    /// </summary>
    public async Task SendClipboardToHost(string sessionId, string text)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(sessionId);
            if (session == null || !CanControl(session)) return;

            if (!string.IsNullOrEmpty(session.HostConnectionId))
            {
                await Clients.Client(session.HostConnectionId)
                    .SendAsync("ExecuteClipboard", text);

                await _sessionService.UpdateActivityAsync(sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при clipboard → host: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Host изпраща clipboard текст към viewer (copy на remote машина)
    /// </summary>
    public async Task SendClipboardToViewer(string sessionId, string text)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(sessionId);
            if (session == null) return;

            if (!string.IsNullOrEmpty(session.ViewerConnectionId))
            {
                await Clients.Client(session.ViewerConnectionId)
                    .SendAsync("ReceiveClipboard", text);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при clipboard → viewer: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Изпраща mouse scroll към host
    /// </summary>
    public async Task SendMouseScroll(string sessionId, int delta)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(sessionId);
            if (session == null || !CanControl(session)) return;

            if (!string.IsNullOrEmpty(session.HostConnectionId))
            {
                await Clients.Client(session.HostConnectionId)
                    .SendAsync("ExecuteMouseScroll", delta);
                await _sessionService.UpdateActivityAsync(sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при mouse scroll: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Изпраща keyboard input към host
    /// </summary>
    public async Task SendKeyPress(string sessionId, string key)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(sessionId);
            if (session == null || !CanControl(session)) return;

            if (!string.IsNullOrEmpty(session.HostConnectionId))
            {
                await Clients.Client(session.HostConnectionId)
                    .SendAsync("ExecuteKeyPress", key);

                await _sessionService.UpdateActivityAsync(sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при key press: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Приключва remote desktop сесия
    /// </summary>
    public async Task DisconnectSession(string sessionId)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(sessionId);
            if (session == null) return;

            if (!string.IsNullOrEmpty(session.HostConnectionId))
            {
                await Clients.Client(session.HostConnectionId)
                    .SendAsync("SessionEnded");
            }

            if (!string.IsNullOrEmpty(session.ViewerConnectionId))
            {
                await Clients.Client(session.ViewerConnectionId)
                    .SendAsync("SessionEnded");
            }

            await _sessionService.EndSessionAsync(sessionId);
            _logger.LogInfo($"Сесия приключена: {sessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при disconnect: {ex.Message}", ex);
        }
    }

    #endregion

    #region Connection Lifecycle

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var sessions = await _sessionService.GetActiveSessionsAsync();
            var session = sessions.FirstOrDefault(s =>
                s.HostConnectionId == Context.ConnectionId ||
                s.ViewerConnectionId == Context.ConnectionId);

            if (session != null)
            {
                _logger.LogInfo($"Connection изключен: Session={session.SessionId}, Connection={Context.ConnectionId}");

                if (session.HostConnectionId == Context.ConnectionId)
                {
                    // Само изчистваме host connection - сесията остава жива за reconnect
                    await _sessionService.ClearHostConnectionAsync(session.SessionId);

                    if (!string.IsNullOrEmpty(session.ViewerConnectionId))
                        await Clients.Client(session.ViewerConnectionId).SendAsync("HostDisconnected");
                }
                else if (session.ViewerConnectionId == Context.ConnectionId)
                {
                    // Само изчистваме viewer connection - сесията остава жива
                    await _sessionService.ClearViewerConnectionAsync(session.SessionId);

                    if (!string.IsNullOrEmpty(session.HostConnectionId))
                        await Clients.Client(session.HostConnectionId).SendAsync("ViewerDisconnected");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при OnDisconnected: {ex.Message}", ex);
        }

        await base.OnDisconnectedAsync(exception);
    }

    #endregion
}
