using System.Diagnostics;
using System.Runtime.InteropServices;
using ADS.WindowsAuth.Core.Services;

namespace ADS.WindowsAuth.Monitor.Services;

/// <summary>
/// Стартира RemoteDesktopHost.exe в потребителската интерактивна сесия.
/// Monitor работи като SYSTEM (Session 0) → не може директно да заснема екрана.
/// Решение: WTSQueryUserToken + CreateProcessAsUser за стартиране в сесията на потребителя.
/// </summary>
public class RemoteDesktopHostService : IDisposable
{
    private readonly ILoggerService _logger;
    private readonly string _apiBaseUrl;
    private Process? _hostProcess;
    private bool _disposed;

    private const string HostExeName = "ADS.WindowsAuth.RemoteDesktopHost.exe";

    public RemoteDesktopHostService(string apiBaseUrl, ILoggerService logger)
    {
        _apiBaseUrl = apiBaseUrl;
        _logger = logger;
    }

    public bool IsRunning => _hostProcess is { HasExited: false };

    /// <summary>
    /// Убива всички стари инстанции на RemoteDesktopHost.exe и стартира нова
    /// в интерактивната сесия на потребителя.
    /// </summary>
    public void EnsureRunning()
    {
        // Убиваме всички стари инстанции (стар бинарен, сринати процеси и т.н.)
        KillExistingInstances();

        string? exePath = FindHostExe();
        if (exePath == null)
        {
            _logger.LogWarning("[RD Host] RemoteDesktopHost.exe не е намерен. Remote Desktop няма да работи.");
            return;
        }

        try
        {
            _hostProcess = LaunchInUserSession(exePath);
            if (_hostProcess != null)
                _logger.LogInfo($"[RD Host] Стартиран в потребителска сесия, PID={_hostProcess.Id}");
            else
                _logger.LogWarning("[RD Host] Неуспешно стартиране в потребителска сесия.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[RD Host] Грешка при стартиране: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Проверява дали процесът е паднал и го рестартира. Извиквай на всеки ~30 сек от Worker.
    /// </summary>
    public void RestartIfDead()
    {
        if (_hostProcess == null || _hostProcess.HasExited)
        {
            _logger.LogWarning("[RD Host] Процесът е спрян – рестартиране...");
            EnsureRunning();
        }
    }

    public void StopIfRunning()
    {
        KillExistingInstances();
        _hostProcess?.Dispose();
        _hostProcess = null;
    }

    // ─── Убиване на стари инстанции ──────────────────────────────────────────

    private void KillExistingInstances()
    {
        try
        {
            var processName = Path.GetFileNameWithoutExtension(HostExeName); // "ADS.WindowsAuth.RemoteDesktopHost"
            var existing = Process.GetProcessesByName(processName);
            foreach (var p in existing)
            {
                try
                {
                    if (_hostProcess != null && p.Id == _hostProcess.Id)
                        continue; // вече следен от нас

                    p.Kill();
                    p.WaitForExit(2000);
                    _logger.LogInfo($"[RD Host] Убит стар процес PID={p.Id}");
                }
                catch { }
                finally { p.Dispose(); }
            }

            // Изчистваме собствен handle ако процесът е паднал
            if (_hostProcess is { HasExited: true })
            {
                _hostProcess.Dispose();
                _hostProcess = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[RD Host] Грешка при KillExistingInstances: {ex.Message}");
        }
    }

    // ─── Търсене на exe ──────────────────────────────────────────────────────

    private string? FindHostExe()
    {
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RemoteDesktopHost", HostExeName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, HostExeName),
            @"C:\ADS\RemoteDesktopHost\" + HostExeName,
            @"C:\Program Files\ADS\RemoteDesktopHost\" + HostExeName,
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                _logger.LogInfo($"[RD Host] Намерен exe: {path}");
                return path;
            }
        }

        return null;
    }

    // ─── Намиране на активна потребителска сесия ─────────────────────────────

    /// <summary>
    /// Връща ID на интерактивна потребителска сесия. Проверява:
    /// 1. Физическата конзолна сесия (WTSGetActiveConsoleSessionId)
    /// 2. Всички активни WTS сесии (обхваща RDP и Fast User Switching)
    /// </summary>
    private uint GetActiveUserSessionId()
    {
        // Опит 1: физическа конзола
        uint consoleId = WTSGetActiveConsoleSessionId();
        if (consoleId != 0xFFFFFFFF && consoleId != 0)
        {
            if (SessionHasUser(consoleId))
                return consoleId;
        }

        // Опит 2: enumerate всички WTS сесии
        IntPtr pSessions = IntPtr.Zero;
        uint count = 0;
        if (WTSEnumerateSessions(IntPtr.Zero, 0, 1, ref pSessions, ref count))
        {
            try
            {
                int size = Marshal.SizeOf<WTS_SESSION_INFO>();
                for (uint i = 0; i < count; i++)
                {
                    var info = Marshal.PtrToStructure<WTS_SESSION_INFO>(pSessions + (int)(i * size));

                    // Търсим активна сесия, различна от Session 0 (services)
                    if (info.State == WTS_CONNECTSTATE_CLASS.WTSActive && info.SessionID != 0)
                    {
                        if (SessionHasUser(info.SessionID))
                        {
                            _logger.LogInfo($"[RD Host] Намерена активна сесия {info.SessionID} ({info.pWinStationName})");
                            return info.SessionID;
                        }
                    }
                }
            }
            finally
            {
                WTSFreeMemory(pSessions);
            }
        }

        // Опит 3: consoleId дори без потвърден потребител (последен шанс)
        if (consoleId != 0xFFFFFFFF)
            return consoleId;

        return 0xFFFFFFFF;
    }

    /// <summary>
    /// Проверява дали в сесията има логнат потребител чрез WTSQueryUserToken.
    /// </summary>
    private static bool SessionHasUser(uint sessionId)
    {
        if (!WTSQueryUserToken(sessionId, out IntPtr token))
            return false;
        CloseHandle(token);
        return true;
    }

    // ─── CreateProcessAsUser (стартиране в потребителска сесия) ──────────────

    private Process? LaunchInUserSession(string exePath)
    {
        uint sessionId = GetActiveUserSessionId();
        if (sessionId == 0xFFFFFFFF)
        {
            _logger.LogWarning("[RD Host] Няма активна потребителска сесия. Очакване...");
            return null;
        }

        _logger.LogInfo($"[RD Host] Стартиране в сесия {sessionId}...");

        if (!WTSQueryUserToken(sessionId, out IntPtr userToken))
        {
            int err = Marshal.GetLastWin32Error();
            _logger.LogWarning($"[RD Host] WTSQueryUserToken неуспешно (error {err}). Опит с Process.Start.");
            return FallbackStart(exePath);
        }

        try
        {
            if (!DuplicateTokenEx(userToken, TOKEN_ALL_ACCESS, IntPtr.Zero,
                    SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                    TOKEN_TYPE.TokenPrimary, out IntPtr primaryToken))
            {
                _logger.LogWarning($"[RD Host] DuplicateTokenEx неуспешно (error {Marshal.GetLastWin32Error()}).");
                return FallbackStart(exePath);
            }

            try
            {
                CreateEnvironmentBlock(out IntPtr envBlock, primaryToken, false);
                try
                {
                    var si = new STARTUPINFO
                    {
                        cb = Marshal.SizeOf<STARTUPINFO>(),
                        lpDesktop = "winsta0\\default",
                        dwFlags = 0x00000001, // STARTF_USESHOWWINDOW
                        wShowWindow = 0       // SW_HIDE – без конзолен прозорец
                    };

                    string cmdLine = $"\"{exePath}\"";
                    string workDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;

                    bool ok = CreateProcessAsUser(
                        primaryToken, null!, cmdLine,
                        IntPtr.Zero, IntPtr.Zero, false,
                        CREATE_UNICODE_ENVIRONMENT | CREATE_NEW_CONSOLE,
                        envBlock, workDir, ref si, out PROCESS_INFORMATION pi);

                    if (!ok)
                    {
                        int err = Marshal.GetLastWin32Error();
                        _logger.LogWarning($"[RD Host] CreateProcessAsUser неуспешно (error {err}).");
                        return FallbackStart(exePath);
                    }

                    CloseHandle(pi.hProcess);
                    CloseHandle(pi.hThread);

                    // Малко изчакване преди GetProcessById, за да е регистриран процесът
                    Thread.Sleep(200);
                    return Process.GetProcessById((int)pi.dwProcessId);
                }
                finally { DestroyEnvironmentBlock(envBlock); }
            }
            finally { CloseHandle(primaryToken); }
        }
        finally { CloseHandle(userToken); }
    }

    private Process? FallbackStart(string exePath)
    {
        try
        {
            _logger.LogInfo("[RD Host] FallbackStart – Process.Start с UseShellExecute");
            return Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError($"[RD Host] FallbackStart неуспешно: {ex.Message}", ex);
            return null;
        }
    }

    // ─── Win32 P/Invoke ───────────────────────────────────────────────────────

    private const uint TOKEN_ALL_ACCESS = 0xF01FF;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const uint CREATE_NEW_CONSOLE = 0x00000010;

    private enum SECURITY_IMPERSONATION_LEVEL { SecurityImpersonation = 2 }
    private enum TOKEN_TYPE { TokenPrimary = 1 }

    private enum WTS_CONNECTSTATE_CLASS
    {
        WTSActive, WTSConnected, WTSConnectQuery, WTSShadow,
        WTSDisconnected, WTSIdle, WTSListen, WTSReset, WTSDown, WTSInit
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WTS_SESSION_INFO
    {
        public uint SessionID;
        [MarshalAs(UnmanagedType.LPWStr)] public string pWinStationName;
        public WTS_CONNECTSTATE_CLASS State;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved, lpDesktop, lpTitle;
        public uint dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public ushort wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public uint dwProcessId, dwThreadId;
    }

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr phToken);

    [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool WTSEnumerateSessions(IntPtr hServer, uint Reserved, uint Version,
        ref IntPtr ppSessionInfo, ref uint pCount);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr pMemory);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(IntPtr hToken, uint dwAccess, IntPtr lpSec,
        SECURITY_IMPERSONATION_LEVEL imp, TOKEN_TYPE type, out IntPtr phNew);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessAsUser(IntPtr hToken, string? app, string cmd,
        IntPtr lpProc, IntPtr lpThread, bool inherit, uint flags, IntPtr env,
        string dir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out IntPtr lpEnv, IntPtr hToken, bool bInherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool DestroyEnvironmentBlock(IntPtr lpEnv);

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr h);

    // ─────────────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopIfRunning();
    }
}
