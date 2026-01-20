
namespace ADS.WindowsAuth.Client;

partial class Form1
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
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
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
        pictureBoxQrCode = new PictureBox();
        labelStatus = new Label();
        labelMachineName = new Label();
        labelUsername = new Label();
        buttonRefresh = new Button();
        buttonInstallCredentialProvider = new Button();
        buttonInstallMonitor = new Button();
        timerStatusCheck = new System.Windows.Forms.Timer(components);
        ((System.ComponentModel.ISupportInitialize)pictureBoxQrCode).BeginInit();
        SuspendLayout();
        // 
        // pictureBoxQrCode
        // 
        pictureBoxQrCode.Location = new Point(12, 86);
        pictureBoxQrCode.Name = "pictureBoxQrCode";
        pictureBoxQrCode.Size = new Size(300, 300);
        pictureBoxQrCode.SizeMode = PictureBoxSizeMode.StretchImage;
        pictureBoxQrCode.TabIndex = 0;
        pictureBoxQrCode.TabStop = false;
        // 
        // labelStatus
        // 
        labelStatus.AutoSize = true;
        labelStatus.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        labelStatus.Location = new Point(12, 9);
        labelStatus.Name = "labelStatus";
        labelStatus.Size = new Size(145, 21);
        labelStatus.TabIndex = 1;
        labelStatus.Text = "Статус: Очакване";
        // 
        // labelMachineName
        // 
        labelMachineName.AutoSize = true;
        labelMachineName.Font = new Font("Segoe UI", 10F);
        labelMachineName.Location = new Point(12, 30);
        labelMachineName.Name = "labelMachineName";
        labelMachineName.Size = new Size(76, 19);
        labelMachineName.TabIndex = 2;
        labelMachineName.Text = "Машина: -";
        // 
        // labelUsername
        // 
        labelUsername.AutoSize = true;
        labelUsername.Font = new Font("Segoe UI", 10F);
        labelUsername.Location = new Point(12, 49);
        labelUsername.Name = "labelUsername";
        labelUsername.Size = new Size(97, 19);
        labelUsername.TabIndex = 3;
        labelUsername.Text = "Потребител: -";
        // 
        // buttonRefresh
        // 
        buttonRefresh.Location = new Point(327, 86);
        buttonRefresh.Name = "buttonRefresh";
        buttonRefresh.Size = new Size(109, 138);
        buttonRefresh.TabIndex = 4;
        buttonRefresh.Text = "Обнови QR код";
        buttonRefresh.UseVisualStyleBackColor = true;
        buttonRefresh.Click += ButtonRefresh_Click;
        // 
        // buttonInstallCredentialProvider
        // 
        buttonInstallCredentialProvider.Location = new Point(327, 247);
        buttonInstallCredentialProvider.Name = "buttonInstallCredentialProvider";
        buttonInstallCredentialProvider.Size = new Size(109, 65);
        buttonInstallCredentialProvider.TabIndex = 5;
        buttonInstallCredentialProvider.Text = "Инсталирай Credential Provider";
        buttonInstallCredentialProvider.UseVisualStyleBackColor = true;
        buttonInstallCredentialProvider.Click += ButtonInstallCredentialProvider_Click;
        // 
        // buttonInstallMonitor
        // 
        buttonInstallMonitor.Location = new Point(327, 321);
        buttonInstallMonitor.Name = "buttonInstallMonitor";
        buttonInstallMonitor.Size = new Size(109, 65);
        buttonInstallMonitor.TabIndex = 6;
        buttonInstallMonitor.Text = "Инсталирай Monitor Service";
        buttonInstallMonitor.UseVisualStyleBackColor = true;
        buttonInstallMonitor.Click += ButtonInstallMonitor_Click;
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
        ClientSize = new Size(448, 398);
        Controls.Add(buttonInstallMonitor);
        Controls.Add(buttonInstallCredentialProvider);
        Controls.Add(buttonRefresh);
        Controls.Add(labelUsername);
        Controls.Add(labelMachineName);
        Controls.Add(labelStatus);
        Controls.Add(pictureBoxQrCode);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        Icon = (Icon)resources.GetObject("$this.Icon");
        MaximizeBox = false;
        Name = "Form1";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Windows Authentication - QR код";
        Load += Form1_Load;
        ((System.ComponentModel.ISupportInitialize)pictureBoxQrCode).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }


    #endregion

    private PictureBox pictureBoxQrCode;
    private Label labelMachineName;
    private Label labelUsername;
    private Button buttonRefresh;
    private Button buttonInstallCredentialProvider;
    private Button buttonInstallMonitor;
    private System.Windows.Forms.Timer timerStatusCheck;
    public Label labelStatus;
}
