using System.Windows.Forms;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Service за симулиране на mouse и keyboard input
/// </summary>
public interface IRemoteInputService
{
    /// <summary>
    /// Премества мишката на определена позиция
    /// </summary>
    void MoveMouse(int x, int y);

    /// <summary>
    /// Симулира mouse click
    /// </summary>
    /// <param name="button">Left, Right или Middle</param>
    void ClickMouse(MouseButtons button);

    /// <summary>
    /// Симулира double click
    /// </summary>
    void DoubleClick(MouseButtons button);

    /// <summary>
    /// Симулира mouse wheel scroll
    /// </summary>
    /// <param name="delta">Scroll amount (positive = up, negative = down)</param>
    void ScrollWheel(int delta);

    /// <summary>
    /// Симулира натискане на клавиш
    /// </summary>
    void SendKeyPress(Keys key);

    /// <summary>
    /// Симулира key down
    /// </summary>
    void SendKeyDown(Keys key);

    /// <summary>
    /// Симулира key up
    /// </summary>
    void SendKeyUp(Keys key);

    /// <summary>
    /// Изпраща текст като последователност от key presses
    /// </summary>
    void SendText(string text);
}
