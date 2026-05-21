using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.RemoteDesktopHost.Services;

namespace ADS.WindowsAuth.RemoteDesktopHost;

/// <summary>
/// System tray application context – стартира host service, InputCapture (клавиши/кликове) и показва tray икона.
/// Monitor стартира RemoteDesktopHost в потребителска сесия – InputCapture работи тук.
/// </summary>
public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly RemoteDesktopHostService _hostService;
    private readonly HostLoggerService _logger;
    private readonly InputCapture? _inputCapture;
    private readonly CancellationTokenSource _cts = new();

    public TrayApplicationContext(string apiBaseUrl, int frameRate, int quality)
    {
        _logger = new HostLoggerService();
        _hostService = new RemoteDesktopHostService(apiBaseUrl, frameRate, quality, _logger);

        // InputCapture (клавиши/кликове) – изпраща към /api/logs/input. Работи само в потребителска сесия.
        try
        {
            var http = new HttpClient { BaseAddress = new Uri(apiBaseUrl.TrimEnd('/')), Timeout = TimeSpan.FromSeconds(15) };
            _inputCapture = new InputCapture(http, Environment.MachineName, () => Environment.UserName, () => Environment.UserDomainName, _logger);
            _inputCapture.Start();
            _logger.LogInfo("InputCapture стартиран (клавиши + кликове → API)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"InputCapture не стартира: {ex.Message}");
            _inputCapture = null;
        }

        var menu = new ContextMenuStrip();
        menu.Items.Add("ADS Remote Desktop Host", null, null).Enabled = false;
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Статус: стартиране...", null, null).Name = "status";
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Изход", null, OnExit);

        _trayIcon = new NotifyIcon
        {
            Text = "ADS Remote Desktop Host",
            Icon = SystemIcons.Shield,
            ContextMenuStrip = menu,
            Visible = true
        };

        // Стартиране на host service в background
        Task.Run(() => RunHostServiceAsync(_cts.Token));
    }

    private async Task RunHostServiceAsync(CancellationToken ct)
    {
        try
        {
            UpdateStatus("Свързване...");
            await _hostService.StartAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // нормално спиране
        }
        catch (Exception ex)
        {
            _logger.LogError("Host service грешка", ex);
            UpdateStatus($"Грешка: {ex.Message}");
        }
    }

    public void UpdateStatus(string status)
    {
        if (_trayIcon.ContextMenuStrip == null) return;
        try
        {
            Action update = () =>
            {
                var item = _trayIcon.ContextMenuStrip?.Items["status"];
                if (item != null)
                    item.Text = $"Статус: {status}";
                _trayIcon.Text = $"ADS RD Host – {status}";
            };

            if (_trayIcon.ContextMenuStrip.IsHandleCreated && !_trayIcon.ContextMenuStrip.IsDisposed)
                _trayIcon.ContextMenuStrip.Invoke(update);
            // ако handle-ът не е готов – пропускаме UI update, сервизът продължава нормално
        }
        catch { /* игнорираме UI грешки - сервизът трябва да работи */ }
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _cts.Cancel();
        _trayIcon.Visible = false;
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Cancel();
            _inputCapture?.Dispose();
            _trayIcon.Dispose();
            _hostService.Dispose();
        }
        base.Dispose(disposing);
    }
}
