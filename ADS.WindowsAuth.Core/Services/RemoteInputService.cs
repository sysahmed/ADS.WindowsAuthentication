using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Имплементация на remote input service използвайки User32
/// </summary>
[SupportedOSPlatform("windows")]
public class RemoteInputService : IRemoteInputService
{
    private readonly ILoggerService _logger;

    // P/Invoke declarations
    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    // Mouse event flags
    private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
    private const uint MOUSEEVENTF_LEFTUP = 0x04;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
    private const uint MOUSEEVENTF_RIGHTUP = 0x10;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x20;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x40;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

    // Keyboard event flags
    private const uint KEYEVENTF_KEYUP = 0x02;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x01;

    public RemoteInputService(ILoggerService logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public void MoveMouse(int x, int y)
    {
        try
        {
            // Валидираме координатите
            var screenSize = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            x = Math.Clamp(x, 0, screenSize.Width - 1);
            y = Math.Clamp(y, 0, screenSize.Height - 1);

            SetCursorPos(x, y);
            _logger.LogInfo($"Mouse moved to ({x}, {y})");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при mouse move: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public void ClickMouse(MouseButtons button)
    {
        try
        {
            uint downFlag = 0, upFlag = 0;

            switch (button)
            {
                case MouseButtons.Left:
                    downFlag = MOUSEEVENTF_LEFTDOWN;
                    upFlag = MOUSEEVENTF_LEFTUP;
                    break;
                case MouseButtons.Right:
                    downFlag = MOUSEEVENTF_RIGHTDOWN;
                    upFlag = MOUSEEVENTF_RIGHTUP;
                    break;
                case MouseButtons.Middle:
                    downFlag = MOUSEEVENTF_MIDDLEDOWN;
                    upFlag = MOUSEEVENTF_MIDDLEUP;
                    break;
                default:
                    _logger.LogWarning($"Unsupported mouse button: {button}");
                    return;
            }

            mouse_event(downFlag, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(10); // Малка пауза за реалистичност
            mouse_event(upFlag, 0, 0, 0, UIntPtr.Zero);

            _logger.LogInfo($"Mouse {button} clicked");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при mouse click: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public void DoubleClick(MouseButtons button)
    {
        try
        {
            ClickMouse(button);
            Thread.Sleep(50);
            ClickMouse(button);
            _logger.LogInfo($"Mouse {button} double-clicked");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при double click: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public void ScrollWheel(int delta)
    {
        try
        {
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, UIntPtr.Zero);
            _logger.LogInfo($"Mouse wheel scrolled: {delta}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при scroll: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public void SendKeyPress(Keys key)
    {
        try
        {
            SendKeyDown(key);
            Thread.Sleep(10);
            SendKeyUp(key);
            _logger.LogInfo($"Key pressed: {key}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при key press: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public void SendKeyDown(Keys key)
    {
        try
        {
            byte vkCode = (byte)key;
            uint flags = IsExtendedKey(key) ? KEYEVENTF_EXTENDEDKEY : 0;
            keybd_event(vkCode, 0, flags, UIntPtr.Zero);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при key down: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public void SendKeyUp(Keys key)
    {
        try
        {
            byte vkCode = (byte)key;
            uint flags = KEYEVENTF_KEYUP;
            if (IsExtendedKey(key))
                flags |= KEYEVENTF_EXTENDEDKEY;
            keybd_event(vkCode, 0, flags, UIntPtr.Zero);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при key up: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public void SendText(string text)
    {
        try
        {
            foreach (char c in text)
            {
                // Използваме SendKeys за по-лесно handling на text
                SendKeys.SendWait(c.ToString());
                Thread.Sleep(10);
            }
            _logger.LogInfo($"Text sent: {text.Length} characters");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при send text: {ex.Message}", ex);
        }
    }

    private static bool IsExtendedKey(Keys key)
    {
        return key switch
        {
            Keys.Up or Keys.Down or Keys.Left or Keys.Right or
            Keys.Home or Keys.End or Keys.Prior or Keys.Next or
            Keys.Insert or Keys.Delete => true,
            _ => false
        };
    }
}
