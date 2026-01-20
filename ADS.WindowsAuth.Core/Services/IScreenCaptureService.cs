using System.Drawing;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Service за заснемане на екрана
/// </summary>
public interface IScreenCaptureService
{
    /// <summary>
    /// Заснема целия екран като JPEG изображение
    /// </summary>
    /// <param name="quality">JPEG качество (1-100)</param>
    /// <param name="width">Целева ширина (0 = original)</param>
    /// <param name="height">Целева височина (0 = original)</param>
    /// <returns>JPEG байтове</returns>
    Task<byte[]> CaptureScreenAsync(int quality = 50, int width = 0, int height = 0);

    /// <summary>
    /// Получава размерите на екрана
    /// </summary>
    /// <returns>Ширина и височина</returns>
    Size GetScreenSize();

    /// <summary>
    /// Заснема специфичен регион от екрана
    /// </summary>
    Task<byte[]> CaptureRegionAsync(Rectangle region, int quality = 50);
}
