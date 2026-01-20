using QRCoder;
using System.Drawing;

namespace ADS.WindowsAuth.Client.Services;

/// <summary>
/// Сервис за генериране на QR кодове
/// </summary>
public class QrCodeService
{
    /// <summary>
    /// Генерира QR код като изображение
    /// </summary>
    /// <param name="data">Данни за кодиране в QR кода</param>
    /// <param name="size">Размер на QR кода в пиксели</param>
    /// <returns>Bitmap изображение на QR кода</returns>
    public Bitmap GenerateQrCode(string data, int size = 300)
    {
        using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
        {
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
            using (QRCode qrCode = new QRCode(qrCodeData))
            {
                // Използваме по-малка стойност (5) за по-гладък и четлив QR код
                // Също така можем да зададем размер и цветове
                return qrCode.GetGraphic(5, System.Drawing.Color.Black, System.Drawing.Color.White, true);
            }
        }
    }
}

