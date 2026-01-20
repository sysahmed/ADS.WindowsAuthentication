using System.Runtime.InteropServices;
using ADS.WindowsAuth.Client.Forms;
using ADS.WindowsAuth.Core.Services;

namespace ADS.WindowsAuth.Client.Services;

/// <summary>
/// Сервис за показване на QR код на lock screen (само C#)
/// </summary>
public class LockScreenService : ILockScreenService
{
    private readonly QrCodeService _qrCodeService;
    private LockScreenQrForm? _lockScreenForm;
    private readonly object _lockObject = new object();

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOACTIVATE = 0x0010;

    public LockScreenService(QrCodeService qrCodeService)
    {
        _qrCodeService = qrCodeService;
    }

    public void ShowQrCode(string qrData)
    {
        lock (_lockObject)
        {
            if (_lockScreenForm == null || _lockScreenForm.IsDisposed)
            {
                _lockScreenForm = new LockScreenQrForm(_qrCodeService);
                _lockScreenForm.UpdateQrCode(qrData);
                
                // Показване на прозореца с най-висок приоритет
                _lockScreenForm.Show();
                _lockScreenForm.WindowState = FormWindowState.Maximized;
                
                // Уверете се, че прозорецът е отгоре
                SetWindowPos(
                    _lockScreenForm.Handle,
                    HWND_TOPMOST,
                    0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
                
                _lockScreenForm.BringToFront();
                _lockScreenForm.Activate();
            }
            else
            {
                _lockScreenForm.UpdateQrCode(qrData);
            }
        }
    }

    public void HideQrCode()
    {
        lock (_lockObject)
        {
            if (_lockScreenForm != null && !_lockScreenForm.IsDisposed)
            {
                _lockScreenForm.Hide();
                _lockScreenForm.Dispose();
                _lockScreenForm = null;
            }
        }
    }

    public void UpdateQrCode(string qrData)
    {
        lock (_lockObject)
        {
            if (_lockScreenForm != null && !_lockScreenForm.IsDisposed)
            {
                _lockScreenForm.UpdateQrCode(qrData);
            }
            else
            {
                ShowQrCode(qrData);
            }
        }
    }
}

