using System.Runtime.InteropServices;

namespace ADS.WindowsAuth.Client.Services;

/// <summary>
/// Помощен клас за по-надеждно показване на прозорец върху lock screen
/// </summary>
public static class WindowsLockScreenHelper
{
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern int AttachThreadInput(int idAttach, int idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern int GetCurrentThreadId();

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const int SW_SHOW = 5;
    private const int SW_MAXIMIZE = 3;
    private const int SW_RESTORE = 9;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_ASYNCWINDOWPOS = 0x4000;

    /// <summary>
    /// По-агресивно показване на прозорец върху lock screen
    /// </summary>
    public static void ForceShowOnTop(IntPtr windowHandle)
    {
        try
        {
            // Метод 1: SetWindowPos с HWND_TOPMOST
            SetWindowPos(
                windowHandle,
                HWND_TOPMOST,
                0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_ASYNCWINDOWPOS);

            // Метод 2: ShowWindow
            ShowWindow(windowHandle, SW_MAXIMIZE);

            // Метод 3: BringWindowToTop
            BringWindowToTop(windowHandle);

            // Метод 4: SetForegroundWindow (може да не работи на lock screen)
            try
            {
                SetForegroundWindow(windowHandle);
            }
            catch
            {
                // Игнорираме ако не работи
            }

            // Метод 5: AttachThreadInput за по-надеждно фокусиране
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow != IntPtr.Zero)
            {
                GetWindowThreadProcessId(foregroundWindow, out int foregroundProcessId);
                int currentThreadId = GetCurrentThreadId();
                GetWindowThreadProcessId(windowHandle, out int windowProcessId);
                
                if (foregroundProcessId != windowProcessId)
                {
                    AttachThreadInput(currentThreadId, GetWindowThreadProcessId(foregroundWindow, out _), true);
                    SetForegroundWindow(windowHandle);
                    AttachThreadInput(currentThreadId, GetWindowThreadProcessId(foregroundWindow, out _), false);
                }
            }
        }
        catch
        {
            // Ако някой метод не работи, продължаваме с другите
        }
    }

    /// <summary>
    /// Проверява дали приложението работи с администраторски права
    /// </summary>
    public static bool IsRunningAsAdministrator()
    {
        try
        {
            System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Проверява дали екранът е заключен
    /// </summary>
    public static bool IsScreenLocked()
    {
        try
        {
            // Проверка чрез Desktop
            IntPtr desktop = GetDesktopWindow();
            IntPtr shell = GetShellWindow();
            
            // Ако няма активен прозорец или е lock screen, считаме че е заключен
            IntPtr foreground = GetForegroundWindow();
            return foreground == IntPtr.Zero || foreground == desktop;
        }
        catch
        {
            return false;
        }
    }
}

