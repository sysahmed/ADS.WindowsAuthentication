using System.Runtime.InteropServices;
using ADS.WindowsAuth.Client.Services;
using ADS.WindowsAuth.Core.Services;

namespace ADS.WindowsAuth.Client.Forms;

/// <summary>
/// Форма за показване на QR код върху lock screen
/// </summary>
public partial class LockScreenQrForm : Form
{
    private readonly QrCodeService _qrCodeService;
    private PictureBox _qrPictureBox = null!;
    private Label _labelInfo = null!;

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    private const int GWL_EXSTYLE = -20;
    private const uint WS_EX_LAYERED = 0x80000;
    private const uint WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOPMOST = 0x8;
    private const uint LWA_ALPHA = 0x2;

    public LockScreenQrForm(QrCodeService qrCodeService)
    {
        _qrCodeService = qrCodeService;
        InitializeComponent();
        SetupForm();
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const int SW_SHOW = 5;
    private const int SW_MAXIMIZE = 3;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOACTIVATE = 0x0010;

    private void SetupForm()
    {
        // Настройване на размера на прозореца според екрана (runtime логика)
        if (Screen.PrimaryScreen != null)
        {
            Size = Screen.PrimaryScreen.Bounds.Size;
        }
        
        // Настройване на прозореца да е видим върху lock screen
        IntPtr handle = Handle;
        uint extendedStyle = GetWindowLong(handle, GWL_EXSTYLE);
        SetWindowLong(handle, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED | WS_EX_TOPMOST);
        SetLayeredWindowAttributes(handle, 0, 255, LWA_ALPHA);

        // По-агресивно поставяне на прозореца отгоре
        WindowsLockScreenHelper.ForceShowOnTop(handle);

        // Скриване на курсора
        Cursor.Hide();
    }

    /// <summary>
    /// Обновява QR кода в прозореца
    /// </summary>
    public void UpdateQrCode(string qrData)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<string>(UpdateQrCode), qrData);
            return;
        }

        try
        {
            Bitmap qrBitmap = _qrCodeService.GenerateQrCode(qrData, 400);
            
            // Създаване на изображение с черен фон и QR код в центъра
            Bitmap finalBitmap = new Bitmap(Width, Height);
            using (Graphics g = Graphics.FromImage(finalBitmap))
            {
                g.Clear(Color.Black);
                
                // Изчисляване на позицията за центриране
                int labelHeight = _labelInfo.Height;
                int x = (Width - qrBitmap.Width) / 2;
                int y = (Height - qrBitmap.Height - labelHeight) / 2;
                
                // Добавяне на бял фон за QR кода
                Rectangle qrRect = new Rectangle(x - 20, y - 20, qrBitmap.Width + 40, qrBitmap.Height + 40);
                using (SolidBrush brush = new SolidBrush(Color.White))
                {
                    g.FillRectangle(brush, qrRect);
                }
                
                g.DrawImage(qrBitmap, x, y);
            }

            _qrPictureBox.Image?.Dispose();
            _qrPictureBox.Image = finalBitmap;
            qrBitmap.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Грешка при обновяване на QR код: {ex.Message}");
        }
    }

    /// <summary>
    /// Показва прозореца
    /// </summary>
    public new void Show()
    {
        base.Show();
        WindowState = FormWindowState.Maximized;
        
        // По-агресивно показване с помощния клас
        WindowsLockScreenHelper.ForceShowOnTop(Handle);
        
        BringToFront();
        Activate();
        
        // Периодично обновяване на позицията (за по-надеждна работа)
        var timer = new System.Windows.Forms.Timer();
        timer.Interval = 1000; // Всеки секунда
        timer.Tick += (s, e) =>
        {
            if (!IsDisposed && Visible)
            {
                WindowsLockScreenHelper.ForceShowOnTop(Handle);
            }
            else
            {
                timer.Stop();
                timer.Dispose();
            }
        };
        timer.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Предотвратяване на затваряне с Alt+F4 или X бутон
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
        }
        base.OnFormClosing(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Позволяване само на специални комбинации за затваряне
        if (e.KeyCode == Keys.Escape && e.Control && e.Shift)
        {
            Cursor.Show();
            base.OnFormClosing(new FormClosingEventArgs(CloseReason.UserClosing, false));
            Close();
        }
        base.OnKeyDown(e);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        // Връщане на фокуса ако прозорецът е деактивиран
        base.OnDeactivate(e);
        BringToFront();
        Activate();
    }
}

