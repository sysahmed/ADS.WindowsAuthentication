using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;

namespace ADS.WindowsAuth.RemoteDesktopHost.Services;

/// <summary>
/// Основен host service:
/// 1. Регистрира сесия в API
/// 2. Свързва се към SignalR hub
/// 3. Изпраща screen frames
/// 4. Изпълнява mouse/keyboard команди от viewer
/// </summary>
public class RemoteDesktopHostService : IDisposable
{
    private readonly string _apiBaseUrl;
    private readonly int _frameRate;
    private readonly HostLoggerService _logger;
    private readonly ScreenCaptureService _capture;
    private HubConnection? _hub;
    private string? _sessionId;
    private string _lastClipboard = string.Empty;
    private int _captureErrorCount = 0;
    private DateTime _lastCaptureErrorLog = DateTime.MinValue;

    // Опашка за input команди – пишат SignalR callbacks (thread pool),
    // чете само dedicated capture нишката (има input desktop достъп)
    private readonly Channel<Action> _inputQueue =
        Channel.CreateBounded<Action>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public RemoteDesktopHostService(string apiBaseUrl, int frameRate, int quality, HostLoggerService logger)
    {
        _apiBaseUrl = apiBaseUrl.TrimEnd('/');
        _frameRate = Math.Max(1, frameRate);
        _logger = logger;
        _capture = new ScreenCaptureService(quality, msg => logger.LogInfo(msg));
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _sessionId = await GetOrCreateSessionAsync(ct);
        _logger.LogInfo($"Сесия: {_sessionId}");

        _hub = new HubConnectionBuilder()
            .WithUrl($"{_apiBaseUrl}/hubs/remotedesktop")
            .WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .Build();

        // Слушаме входящи команди от viewer
        _hub.On<int, int>("ExecuteMouseMove", ExecuteMouseMove);
        _hub.On<string>("ExecuteMouseClick", ExecuteMouseClick);
        _hub.On<int>("ExecuteMouseScroll", ExecuteMouseScroll);
        _hub.On<string>("ExecuteKeyPress", ExecuteKeyPress);
        _hub.On<string>("ExecuteClipboard", ExecuteClipboard);
        _hub.On("SessionEnded", () => _logger.LogInfo("Сесията приключи от viewer"));
        _hub.On("ViewerDisconnected", () => _logger.LogInfo("Viewer се изключи"));

        _hub.Reconnecting += ex =>
        {
            _logger.LogWarning($"Прекъсване, повторно свързване: {ex?.Message}");
            return Task.CompletedTask;
        };

        _hub.Reconnected += async connectionId =>
        {
            _logger.LogInfo($"Повторно свързан: {connectionId}");
            await RegisterHostAsync();
        };

        await _hub.StartAsync(ct);
        await RegisterHostAsync();

        _logger.LogInfo($"Host готов. Изпращане на frames @ {_frameRate} FPS");

        _ = ClipboardWatchLoopAsync(ct);

        // Capture loop на DEDICATED нишка (не thread pool!) за да работи SetThreadDesktop
        await Task.Factory.StartNew(
            () => CaptureLoopCore(ct),
            ct,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private async Task RegisterHostAsync()
    {
        if (_hub == null || _sessionId == null) return;
        try
        {
            await _hub.InvokeAsync("RegisterHost", _sessionId, true); // autoApprove = true
            _logger.LogInfo("RegisterHost успешно");
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при RegisterHost", ex);
        }
    }

    /// <summary>
    /// Работи на DEDICATED LongRunning нишка – не thread pool!
    /// SetThreadDesktop се проваля на thread pool нишки с USER handles.
    /// </summary>
    private void CaptureLoopCore(CancellationToken ct)
    {
        _logger.LogInfo($"[Capture] Нишка стартирана. Session={System.Diagnostics.Process.GetCurrentProcess().SessionId}");

        var interval = TimeSpan.FromMilliseconds(1000.0 / _frameRate);

        while (!ct.IsCancellationRequested)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (_hub?.State == HubConnectionState.Connected && _sessionId != null)
                {
                    var frame = _capture.CaptureScreen();
                    // Empty = DXGI timeout (no new frame) – skip sending to avoid redundant traffic
                    if (frame.Length > 0)
                    {
                        _hub.InvokeAsync("SendScreenFrameBySession", _sessionId, frame, ct)
                            .GetAwaiter().GetResult();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _captureErrorCount++;
                if ((DateTime.Now - _lastCaptureErrorLog).TotalSeconds >= 10)
                {
                    _logger.LogError($"Грешка при capture (общо {_captureErrorCount}x)", ex);
                    _lastCaptureErrorLog = DateTime.Now;
                    _captureErrorCount = 0;
                }
                Thread.Sleep(500);
            }

            // Изпълняваме всички натрупани input команди от SignalR callbacks.
            // Важно: тук сме на dedicated нишката с input desktop достъп.
            while (_inputQueue.Reader.TryRead(out var inputAction))
            {
                try { inputAction(); }
                catch (Exception ex) { _logger.LogError("Input грешка", ex); }
            }

            sw.Stop();
            var delay = interval - sw.Elapsed;
            if (delay > TimeSpan.Zero)
                Thread.Sleep(delay);
        }
    }

    /// <summary>
    /// Следи clipboard на host машината и при промяна изпраща към viewer.
    /// </summary>
    private async Task ClipboardWatchLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, ct);

                if (_hub?.State != HubConnectionState.Connected || _sessionId == null)
                    continue;

                string current = GetClipboardText();
                if (string.IsNullOrEmpty(current) || current == _lastClipboard)
                    continue;

                _lastClipboard = current;
                await _hub.InvokeAsync("SendClipboardToViewer", _sessionId, current, ct);
            }
            catch (OperationCanceledException) { break; }
            catch { /* тихо игнорираме clipboard грешки */ }
        }
    }

    /// <summary>
    /// Получава clipboard текст от viewer и го поставя в Windows clipboard, после Ctrl+V.
    /// </summary>
    private void ExecuteClipboard(string text)
    {
        // SetClipboardText може да се вика от всяка нишка
        try
        {
            SetClipboardText(text);
            _lastClipboard = text;
            _logger.LogInfo($"Clipboard получен от viewer ({text.Length} символа)");
        }
        catch (Exception ex)
        {
            _logger.LogError("SetClipboard грешка", ex);
            return;
        }

        // Ctrl+V трябва от dedicated нишката с input desktop
        _inputQueue.Writer.TryWrite(() =>
        {
            var ctrlDown = new INPUT { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = VK_CONTROL } };
            var vDown    = new INPUT { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = VK_V } };
            var vUp      = new INPUT { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = VK_V,       dwFlags = KEYEVENTF_KEYUP } };
            var ctrlUp   = new INPUT { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = VK_CONTROL, dwFlags = KEYEVENTF_KEYUP } };

            SendInput(1, ref ctrlDown, Marshal.SizeOf(ctrlDown));
            SendInput(1, ref vDown,    Marshal.SizeOf(vDown));
            SendInput(1, ref vUp,      Marshal.SizeOf(vUp));
            SendInput(1, ref ctrlUp,   Marshal.SizeOf(ctrlUp));
        });
    }

    // ─── Win32 Clipboard ────────────────────────────────────────────────────

    private static string GetClipboardText()
    {
        if (!OpenClipboard(IntPtr.Zero)) return string.Empty;
        try
        {
            IntPtr hData = GetClipboardData(CF_UNICODETEXT);
            if (hData == IntPtr.Zero) return string.Empty;
            IntPtr ptr = GlobalLock(hData);
            if (ptr == IntPtr.Zero) return string.Empty;
            try { return Marshal.PtrToStringUni(ptr) ?? string.Empty; }
            finally { GlobalUnlock(hData); }
        }
        finally { CloseClipboard(); }
    }

    private static void SetClipboardText(string text)
    {
        if (!OpenClipboard(IntPtr.Zero)) return;
        try
        {
            EmptyClipboard();
            int bytes = (text.Length + 1) * 2;
            IntPtr hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
            if (hMem == IntPtr.Zero) return;
            IntPtr ptr = GlobalLock(hMem);
            try { Marshal.Copy(text.ToCharArray(), 0, ptr, text.Length); }
            finally { GlobalUnlock(hMem); }
            SetClipboardData(CF_UNICODETEXT, hMem);
        }
        finally { CloseClipboard(); }
    }

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE  = 0x0002;

    [DllImport("user32.dll")] private static extern bool   OpenClipboard(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool   CloseClipboard();
    [DllImport("user32.dll")] private static extern bool   EmptyClipboard();
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint uFormat);
    [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern bool   GlobalUnlock(IntPtr hMem);

    private async Task<string> GetOrCreateSessionAsync(CancellationToken ct)
    {
        var machineName = Environment.MachineName;

        // Игнорираме TLS грешки за self-signed сертификати (dev environment)
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler) { BaseAddress = new Uri(_apiBaseUrl) };

        var response = await http.PostAsJsonAsync(
            "/api/remotedesktop/sessions/createorget",
            new { machineName, requestedBy = machineName },
            ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SessionResponse>(cancellationToken: ct);
        return result?.SessionId ?? throw new Exception("API не върна sessionId");
    }

    #region Input Execution (Win32)

    private void ExecuteMouseMove(int x, int y)
    {
        _inputQueue.Writer.TryWrite(() =>
        {
            var screen = Screen.PrimaryScreen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
            int absX = (int)((x * 65535.0) / screen.Width);
            int absY = (int)((y * 65535.0) / screen.Height);

            var input = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT { dx = absX, dy = absY, dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE }
            };
            if (SendInput(1, ref input, Marshal.SizeOf(input)) == 0)
                _logger.LogWarning($"MouseMove SendInput фейлна: {Marshal.GetLastWin32Error()}");
        });
    }

    private void ExecuteMouseScroll(int delta)
    {
        _inputQueue.Writer.TryWrite(() =>
        {
            var input = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    mouseData = (uint)(delta < 0 ? unchecked((uint)-120) : 120u),
                    dwFlags = MOUSEEVENTF_WHEEL
                }
            };
            if (SendInput(1, ref input, Marshal.SizeOf(input)) == 0)
                _logger.LogWarning($"MouseScroll SendInput фейлна: {Marshal.GetLastWin32Error()}");
        });
    }

    private void ExecuteMouseClick(string button)
    {
        _inputQueue.Writer.TryWrite(() =>
        {
            uint downFlag, upFlag;
            switch (button.ToLower())
            {
                case "right":  downFlag = MOUSEEVENTF_RIGHTDOWN;  upFlag = MOUSEEVENTF_RIGHTUP;  break;
                case "middle": downFlag = MOUSEEVENTF_MIDDLEDOWN; upFlag = MOUSEEVENTF_MIDDLEUP; break;
                default:       downFlag = MOUSEEVENTF_LEFTDOWN;   upFlag = MOUSEEVENTF_LEFTUP;   break;
            }

            var down = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = downFlag } };
            var up   = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = upFlag } };

            if (SendInput(1, ref down, Marshal.SizeOf(down)) == 0 ||
                SendInput(1, ref up,   Marshal.SizeOf(up))   == 0)
                _logger.LogWarning($"MouseClick SendInput фейлна: {Marshal.GetLastWin32Error()}");
        });
    }

    private void ExecuteKeyPress(string key)
    {
        _inputQueue.Writer.TryWrite(() =>
        {
            if (key.Length == 1)
            {
                var down = new INPUT { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wScan = (ushort)key[0], dwFlags = KEYEVENTF_UNICODE } };
                var up   = new INPUT { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wScan = (ushort)key[0], dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP } };
                SendInput(1, ref down, Marshal.SizeOf(down));
                SendInput(1, ref up,   Marshal.SizeOf(up));
            }
            else
            {
                var vk = key switch
                {
                    "Enter"     => VK_RETURN,
                    "Backspace" => VK_BACK,
                    "Escape"    => VK_ESCAPE,
                    "Tab"       => VK_TAB,
                    "Delete"    => VK_DELETE,
                    "ArrowLeft" => VK_LEFT,
                    "ArrowRight"=> VK_RIGHT,
                    "ArrowUp"   => VK_UP,
                    "ArrowDown" => VK_DOWN,
                    "Home"      => VK_HOME,
                    "End"       => VK_END,
                    "PageUp"    => VK_PRIOR,
                    "PageDown"  => VK_NEXT,
                    _ => (ushort)0
                };

                if (vk == 0) return;

                var down = new INPUT { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = vk } };
                var up   = new INPUT { type = INPUT_KEYBOARD, ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } };

                if (SendInput(1, ref down, Marshal.SizeOf(down)) == 0 ||
                    SendInput(1, ref up,   Marshal.SizeOf(up))   == 0)
                    _logger.LogWarning($"KeyPress SendInput фейлна ({key}): {Marshal.GetLastWin32Error()}");
            }
        });
    }

    #endregion

    #region Win32 Interop

    private const int INPUT_MOUSE = 0;
    private const int INPUT_KEYBOARD = 1;

    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V      = 0x56;
    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_BACK = 0x08;
    private const ushort VK_ESCAPE = 0x1B;
    private const ushort VK_TAB = 0x09;
    private const ushort VK_DELETE = 0x2E;
    private const ushort VK_LEFT = 0x25;
    private const ushort VK_RIGHT = 0x27;
    private const ushort VK_UP = 0x26;
    private const ushort VK_DOWN = 0x28;
    private const ushort VK_HOME = 0x24;
    private const ushort VK_END = 0x23;
    private const ushort VK_PRIOR = 0x21;
    private const ushort VK_NEXT = 0x22;

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL, wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUT
    {
        [FieldOffset(0)] public int type;
        [FieldOffset(4)] public MOUSEINPUT mi;
        [FieldOffset(4)] public KEYBDINPUT ki;
        [FieldOffset(4)] public HARDWAREINPUT hi;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);

    #endregion

    public void Dispose()
    {
        _capture.Dispose();
        _hub?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(3));
    }

    private record SessionResponse(string SessionId);
}
