namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Интерфейс за показване на QR код на lock screen
/// </summary>
public interface ILockScreenService
{
    /// <summary>
    /// Показва QR код на lock screen
    /// </summary>
    void ShowQrCode(string qrData);

    /// <summary>
    /// Скрива QR кода
    /// </summary>
    void HideQrCode();

    /// <summary>
    /// Обновява QR кода
    /// </summary>
    void UpdateQrCode(string qrData);
}

