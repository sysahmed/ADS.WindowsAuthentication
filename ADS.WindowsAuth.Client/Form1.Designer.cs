
namespace ADS.WindowsAuth.Client;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
        panelHeader = new Panel();
        labelTitle = new Label();
        labelStatus = new Label();
        panelStatus = new Panel();
        labelUsername = new Label();
        labelMachineName = new Label();
        panelQrCard = new Panel();
        labelQrHint = new Label();
        pictureBoxQrCode = new PictureBox();
        panelActions = new Panel();
        buttonRefresh = new Button();
        buttonInstallCredentialProvider = new Button();
        buttonInstallMonitor = new Button();
        buttonInstallRemoteDesktop = new Button();
        timerStatusCheck = new System.Windows.Forms.Timer(components);
        panelHeader.SuspendLayout();
        panelQrCard.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)pictureBoxQrCode).BeginInit();
        panelActions.SuspendLayout();
        SuspendLayout();
        // 
        // panelHeader
        // 
        panelHeader.BackColor = Color.FromArgb(30, 58, 95);
        panelHeader.Controls.Add(labelTitle);
        panelHeader.Controls.Add(labelStatus);
        panelHeader.Controls.Add(panelStatus);
        panelHeader.Controls.Add(labelUsername);
        panelHeader.Controls.Add(labelMachineName);
        panelHeader.Dock = DockStyle.Top;
        panelHeader.Location = new Point(0, 0);
        panelHeader.Name = "panelHeader";
        panelHeader.Padding = new Padding(20, 16, 20, 16);
        panelHeader.Size = new Size(480, 110);
        panelHeader.TabIndex = 0;
        // 
        // labelTitle
        // 
        labelTitle.AutoSize = true;
        labelTitle.Font = new Font("Segoe UI Semibold", 14F);
        labelTitle.ForeColor = Color.White;
        labelTitle.Location = new Point(20, 12);
        labelTitle.Name = "labelTitle";
        labelTitle.Size = new Size(265, 25);
        labelTitle.TabIndex = 0;
        labelTitle.Text = "ADS Windows Authentication";
        // 
        // labelStatus
        // 
        labelStatus.AutoSize = true;
        labelStatus.Font = new Font("Segoe UI", 9.75F);
        labelStatus.ForeColor = Color.White;
        labelStatus.Location = new Point(36, 80);
        labelStatus.Name = "labelStatus";
        labelStatus.Size = new Size(110, 17);
        labelStatus.TabIndex = 4;
        labelStatus.Text = "Статус: Очакване";
        // 
        // panelStatus
        // 
        panelStatus.BackColor = Color.FromArgb(255, 193, 7);
        panelStatus.Location = new Point(20, 84);
        panelStatus.Name = "panelStatus";
        panelStatus.Size = new Size(8, 8);
        panelStatus.TabIndex = 3;
        // 
        // labelUsername
        // 
        labelUsername.AutoSize = true;
        labelUsername.Font = new Font("Segoe UI", 9.75F);
        labelUsername.ForeColor = Color.FromArgb(220, 220, 220);
        labelUsername.Location = new Point(20, 62);
        labelUsername.Name = "labelUsername";
        labelUsername.Size = new Size(91, 17);
        labelUsername.TabIndex = 2;
        labelUsername.Text = "Потребител: -";
        // 
        // labelMachineName
        // 
        labelMachineName.AutoSize = true;
        labelMachineName.Font = new Font("Segoe UI", 9.75F);
        labelMachineName.ForeColor = Color.FromArgb(220, 220, 220);
        labelMachineName.Location = new Point(20, 42);
        labelMachineName.Name = "labelMachineName";
        labelMachineName.Size = new Size(71, 17);
        labelMachineName.TabIndex = 1;
        labelMachineName.Text = "Машина: -";
        // 
        // panelQrCard
        // 
        panelQrCard.BackColor = Color.White;
        panelQrCard.Controls.Add(labelQrHint);
        panelQrCard.Controls.Add(pictureBoxQrCode);
        panelQrCard.Location = new Point(20, 130);
        panelQrCard.Name = "panelQrCard";
        panelQrCard.Padding = new Padding(16);
        panelQrCard.Size = new Size(320, 340);
        panelQrCard.TabIndex = 1;
        panelQrCard.Paint += PanelQrCard_Paint;
        // 
        // labelQrHint
        // 
        labelQrHint.Dock = DockStyle.Bottom;
        labelQrHint.Font = new Font("Segoe UI", 9F);
        labelQrHint.ForeColor = Color.FromArgb(107, 114, 128);
        labelQrHint.Location = new Point(16, 300);
        labelQrHint.Name = "labelQrHint";
        labelQrHint.Padding = new Padding(0, 8, 0, 0);
        labelQrHint.Size = new Size(288, 24);
        labelQrHint.TabIndex = 1;
        labelQrHint.Text = "Сканирай с мобилното приложение";
        labelQrHint.TextAlign = ContentAlignment.TopCenter;
        // 
        // pictureBoxQrCode
        // 
        pictureBoxQrCode.BackColor = Color.White;
        pictureBoxQrCode.Location = new Point(16, 16);
        pictureBoxQrCode.Name = "pictureBoxQrCode";
        pictureBoxQrCode.Size = new Size(288, 280);
        pictureBoxQrCode.SizeMode = PictureBoxSizeMode.StretchImage;
        pictureBoxQrCode.TabIndex = 0;
        pictureBoxQrCode.TabStop = false;
        // 
        // panelActions
        // 
        panelActions.Controls.Add(buttonRefresh);
        panelActions.Controls.Add(buttonInstallCredentialProvider);
        panelActions.Controls.Add(buttonInstallMonitor);
        panelActions.Controls.Add(buttonInstallRemoteDesktop);
        panelActions.Location = new Point(350, 130);
        panelActions.Name = "panelActions";
        panelActions.Size = new Size(120, 460);
        panelActions.TabIndex = 2;
        // 
        // buttonRefresh
        // 
        buttonRefresh.BackColor = Color.FromArgb(59, 130, 246);
        buttonRefresh.Cursor = Cursors.Hand;
        buttonRefresh.FlatAppearance.BorderSize = 0;
        buttonRefresh.FlatAppearance.MouseOverBackColor = Color.FromArgb(37, 99, 235);
        buttonRefresh.FlatStyle = FlatStyle.Flat;
        buttonRefresh.Font = new Font("Segoe UI Semibold", 9.75F);
        buttonRefresh.ForeColor = Color.White;
        buttonRefresh.Location = new Point(0, 0);
        buttonRefresh.Name = "buttonRefresh";
        buttonRefresh.Size = new Size(120, 80);
        buttonRefresh.TabIndex = 0;
        buttonRefresh.Text = "Обнови\r\nQR код";
        buttonRefresh.UseVisualStyleBackColor = false;
        buttonRefresh.Click += ButtonRefresh_Click;
        // 
        // buttonInstallCredentialProvider
        // 
        buttonInstallCredentialProvider.BackColor = Color.FromArgb(34, 139, 34);
        buttonInstallCredentialProvider.Cursor = Cursors.Hand;
        buttonInstallCredentialProvider.FlatAppearance.BorderSize = 0;
        buttonInstallCredentialProvider.FlatAppearance.MouseOverBackColor = Color.FromArgb(28, 117, 28);
        buttonInstallCredentialProvider.FlatStyle = FlatStyle.Flat;
        buttonInstallCredentialProvider.Font = new Font("Segoe UI", 8.25F);
        buttonInstallCredentialProvider.ForeColor = Color.White;
        buttonInstallCredentialProvider.Location = new Point(0, 88);
        buttonInstallCredentialProvider.Name = "buttonInstallCredentialProvider";
        buttonInstallCredentialProvider.Size = new Size(120, 118);
        buttonInstallCredentialProvider.TabIndex = 1;
        buttonInstallCredentialProvider.Text = "Инсталирай Credential Provider";
        buttonInstallCredentialProvider.UseVisualStyleBackColor = false;
        buttonInstallCredentialProvider.Click += ButtonInstallCredentialProvider_Click;
        // 
        // buttonInstallMonitor
        // 
        buttonInstallMonitor.BackColor = Color.FromArgb(100, 100, 120);
        buttonInstallMonitor.Cursor = Cursors.Hand;
        buttonInstallMonitor.FlatAppearance.BorderSize = 0;
        buttonInstallMonitor.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 100);
        buttonInstallMonitor.FlatStyle = FlatStyle.Flat;
        buttonInstallMonitor.Font = new Font("Segoe UI", 8.25F);
        buttonInstallMonitor.ForeColor = Color.White;
        buttonInstallMonitor.Location = new Point(0, 214);
        buttonInstallMonitor.Name = "buttonInstallMonitor";
        buttonInstallMonitor.Size = new Size(120, 118);
        buttonInstallMonitor.TabIndex = 2;
        buttonInstallMonitor.Text = "Инсталирай Monitor Service";
        buttonInstallMonitor.UseVisualStyleBackColor = false;
        buttonInstallMonitor.Click += ButtonInstallMonitor_Click;
        //
        // buttonInstallRemoteDesktop
        //
        buttonInstallRemoteDesktop.BackColor = Color.FromArgb(37, 99, 200);
        buttonInstallRemoteDesktop.Cursor = Cursors.Hand;
        buttonInstallRemoteDesktop.FlatAppearance.BorderSize = 0;
        buttonInstallRemoteDesktop.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 160);
        buttonInstallRemoteDesktop.FlatStyle = FlatStyle.Flat;
        buttonInstallRemoteDesktop.Font = new Font("Segoe UI", 8.25F);
        buttonInstallRemoteDesktop.ForeColor = Color.White;
        buttonInstallRemoteDesktop.Location = new Point(0, 340);
        buttonInstallRemoteDesktop.Name = "buttonInstallRemoteDesktop";
        buttonInstallRemoteDesktop.Size = new Size(120, 118);
        buttonInstallRemoteDesktop.TabIndex = 3;
        buttonInstallRemoteDesktop.Text = "Инсталирай Remote Desktop";
        buttonInstallRemoteDesktop.UseVisualStyleBackColor = false;
        buttonInstallRemoteDesktop.Click += ButtonInstallRemoteDesktop_Click;
        //
        // timerStatusCheck
        // 
        timerStatusCheck.Interval = 2000;
        timerStatusCheck.Tick += TimerStatusCheck_Tick;
        // 
        // Form1
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(243, 244, 246);
        ClientSize = new Size(480, 610);
        Controls.Add(panelActions);
        Controls.Add(panelQrCard);
        Controls.Add(panelHeader);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        Icon = (Icon)resources.GetObject("$this.Icon");
        MaximizeBox = false;
        MinimumSize = new Size(496, 649);
        Name = "Form1";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "ADS Windows Auth - QR вход";
        Load += Form1_Load;
        panelHeader.ResumeLayout(false);
        panelHeader.PerformLayout();
        panelQrCard.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)pictureBoxQrCode).EndInit();
        panelActions.ResumeLayout(false);
        ResumeLayout(false);
    }

    private void PanelQrCard_Paint(object? sender, PaintEventArgs e)
    {
        using var pen = new Pen(Color.FromArgb(229, 231, 235), 1);
        var r = panelQrCard.ClientRectangle;
        r.Inflate(-1, -1);
        e.Graphics.DrawRectangle(pen, 0, 0, r.Width - 1, r.Height - 1);
    }

    #endregion

    private Panel panelHeader;
    private Label labelTitle;
    private Label labelMachineName;
    private Label labelUsername;
    private Panel panelStatus;
    private Panel panelQrCard;
    private Label labelQrHint;
    private PictureBox pictureBoxQrCode;
    private Panel panelActions;
    private Button buttonRefresh;
    private Button buttonInstallCredentialProvider;
    private Button buttonInstallMonitor;
    private Button buttonInstallRemoteDesktop;
    private System.Windows.Forms.Timer timerStatusCheck;
    public Label labelStatus;
}
