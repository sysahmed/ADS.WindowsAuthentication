namespace ADS.WindowsAuth.Client.Forms;

partial class LockScreenQrForm
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        _qrPictureBox = new PictureBox();
        _labelInfo = new Label();
        ((System.ComponentModel.ISupportInitialize)_qrPictureBox).BeginInit();
        SuspendLayout();
        // 
        // _qrPictureBox
        // 
        _qrPictureBox.BackColor = Color.White;
        _qrPictureBox.Dock = DockStyle.Fill;
        _qrPictureBox.Image = Properties.Resources.Screenshot_2025_10_02_113836;
        _qrPictureBox.Location = new Point(0, 0);
        _qrPictureBox.Name = "_qrPictureBox";
        _qrPictureBox.Padding = new Padding(20);
        _qrPictureBox.Size = new Size(1920, 1020);
        _qrPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        _qrPictureBox.TabIndex = 0;
        _qrPictureBox.TabStop = false;
        // 
        // _labelInfo
        // 
        _labelInfo.BackColor = Color.FromArgb(200, 0, 0, 0);
        _labelInfo.Dock = DockStyle.Bottom;
        _labelInfo.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
        _labelInfo.ForeColor = Color.White;
        _labelInfo.Location = new Point(0, 1020);
        _labelInfo.Name = "_labelInfo";
        _labelInfo.Size = new Size(1920, 60);
        _labelInfo.TabIndex = 1;
        _labelInfo.Text = "Сканирайте QR кода с мобилното приложение";
        _labelInfo.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // LockScreenQrForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.Black;
        ClientSize = new Size(1920, 1080);
        Controls.Add(_qrPictureBox);
        Controls.Add(_labelInfo);
        FormBorderStyle = FormBorderStyle.None;
        Name = "LockScreenQrForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        WindowState = FormWindowState.Maximized;
        ((System.ComponentModel.ISupportInitialize)_qrPictureBox).EndInit();
        ResumeLayout(false);
    }

    #endregion
}

