using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Имплементация на screen capture service използвайки GDI32
/// </summary>
[SupportedOSPlatform("windows")]
public class ScreenCaptureService : IScreenCaptureService
{
    private readonly ILoggerService _logger;

    // P/Invoke declarations за screen capture
    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest,
        int wDest, int hDest, IntPtr hdcSource, int xSrc, int ySrc, int rop);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private const int SRCCOPY = 0x00CC0020;

    public ScreenCaptureService(ILoggerService logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Size GetScreenSize()
    {
        try
        {
            return new Size(
                System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width,
                System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height
            );
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при получаване на размер на екрана: {ex.Message}", ex);
            return new Size(1920, 1080); // Fallback default
        }
    }

    /// <inheritdoc/>
    public async Task<byte[]> CaptureScreenAsync(int quality = 50, int width = 0, int height = 0)
    {
        return await Task.Run(() =>
        {
            var screenSize = GetScreenSize();
            var region = new Rectangle(0, 0, screenSize.Width, screenSize.Height);
            return CaptureRegion(region, quality, width, height);
        });
    }

    /// <inheritdoc/>
    public async Task<byte[]> CaptureRegionAsync(Rectangle region, int quality = 50)
    {
        return await Task.Run(() => CaptureRegion(region, quality, 0, 0));
    }

    private byte[] CaptureRegion(Rectangle region, int quality, int targetWidth, int targetHeight)
    {
        IntPtr desktopWindow = IntPtr.Zero;
        IntPtr desktopDC = IntPtr.Zero;
        IntPtr memoryDC = IntPtr.Zero;
        IntPtr bitmap = IntPtr.Zero;
        IntPtr oldBitmap = IntPtr.Zero;

        try
        {
            // Получаваме desktop window и DC
            desktopWindow = GetDesktopWindow();
            desktopDC = GetWindowDC(desktopWindow);

            // Създаваме memory DC и bitmap
            memoryDC = CreateCompatibleDC(desktopDC);
            bitmap = CreateCompatibleBitmap(desktopDC, region.Width, region.Height);
            oldBitmap = SelectObject(memoryDC, bitmap);

            // Копираме екрана в bitmap
            bool success = BitBlt(memoryDC, 0, 0, region.Width, region.Height,
                desktopDC, region.X, region.Y, SRCCOPY);

            if (!success)
            {
                _logger.LogWarning("BitBlt failed during screen capture");
                return Array.Empty<byte>();
            }

            // Конвертираме в .NET Bitmap
            using var image = Image.FromHbitmap(bitmap);

            // Resize ако е необходимо
            Image finalImage = image;
            if (targetWidth > 0 && targetHeight > 0)
            {
                finalImage = new Bitmap(image, new Size(targetWidth, targetHeight));
            }

            // Компресираме като JPEG
            using var ms = new MemoryStream();
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(
                System.Drawing.Imaging.Encoder.Quality, (long)quality);

            var jpegCodec = GetJpegEncoder();
            finalImage.Save(ms, jpegCodec, encoderParams);

            if (finalImage != image)
            {
                finalImage.Dispose();
            }

            return ms.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при screen capture: {ex.Message}", ex);
            return Array.Empty<byte>();
        }
        finally
        {
            // Cleanup GDI resources
            if (oldBitmap != IntPtr.Zero)
                SelectObject(memoryDC, oldBitmap);
            if (bitmap != IntPtr.Zero)
                DeleteObject(bitmap);
            if (memoryDC != IntPtr.Zero)
                DeleteDC(memoryDC);
            if (desktopDC != IntPtr.Zero)
                ReleaseDC(desktopWindow, desktopDC);
        }
    }

    private static ImageCodecInfo GetJpegEncoder()
    {
        var codecs = ImageCodecInfo.GetImageEncoders();
        foreach (var codec in codecs)
        {
            if (codec.FormatID == ImageFormat.Jpeg.Guid)
            {
                return codec;
            }
        }
        throw new Exception("JPEG encoder не е намерен");
    }
}
