using ADS.WindowsAuth.Client.Forms;
using ADS.WindowsAuth.Client.Services;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;
using Microsoft.Win32;
using System.Text.Json;

namespace ADS.WindowsAuth.Client;

public partial class Form1 : Form
{
    private readonly QrCodeService _qrCodeService;
    private readonly ApiClient _apiClient;
    private readonly ILoggerService _logger;
    private readonly CredentialProviderInstallerService _installerService;
    private readonly MonitorInstallerService _monitorInstallerService;
    private readonly RemoteDesktopInstallerService _rdInstallerService;
    private readonly string _apiUrl;
    private AuthSession? _currentSession;
    private string _applicationDirectory;
    private LockScreenQrForm? _lockScreenForm;

    public Form1()
    {
        InitializeComponent();
        
        _applicationDirectory = Application.StartupPath;
        _apiUrl = LoadApiUrlFromConfig();
        _logger = new LoggerService(_applicationDirectory);
        _qrCodeService = new QrCodeService();
        _installerService = new CredentialProviderInstallerService(_logger);
        _monitorInstallerService = new MonitorInstallerService(_logger);
        _rdInstallerService = new RemoteDesktopInstallerService(_logger);
        _apiClient = new ApiClient(_apiUrl, _logger);
        
        // Слушане за промени в сесията (lock/unlock)
        SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        
        _logger.LogInfo($"Windows Authentication Client стартиран. API URL: {_apiUrl}");
    }

    /// <summary>
    /// Зарежда API URL от appsettings.json или appsettings.Development.json (ако е Debug режим) или използва default стойност
    /// </summary>
    private string LoadApiUrlFromConfig()
    {
        string? url = null;
        
        // Първо проверяваме за Development конфигурация (ако е Debug режим)
        // Забележка: не използваме _logger тук – метода се вика преди създаването му
        #if DEBUG
        try
        {
            string devConfigPath = Path.Combine(_applicationDirectory, "appsettings.Development.json");
            if (File.Exists(devConfigPath))
            {
                string json = File.ReadAllText(devConfigPath);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("ApiConfiguration", out JsonElement apiConfig))
                    {
                        if (apiConfig.TryGetProperty("BaseUrl", out JsonElement baseUrl))
                        {
                            url = baseUrl.GetString();
                            if (!string.IsNullOrEmpty(url))
                                return url;
                        }
                    }
                }
            }
        }
        catch { /* игнорираме – ще опитаме appsettings.json */ }
        #endif
        
        // След това проверяваме основния appsettings.json
        try
        {
            string configPath = Path.Combine(_applicationDirectory, "appsettings.json");
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("ApiConfiguration", out JsonElement apiConfig))
                    {
                        if (apiConfig.TryGetProperty("BaseUrl", out JsonElement baseUrl))
                        {
                            url = baseUrl.GetString();
                            if (!string.IsNullOrEmpty(url))
                                return url;
                        }
                    }
                }
            }
        }
        catch { /* игнорираме – ще използваме default */ }
        
        // Default стойност - ако няма конфигурация, използваме production URL
        return "https://ads-auth.nursanbulgaria.com";
    }

    private async void Form1_Load(object sender, EventArgs e)
    {
        await RefreshQrCodeAsync();
    }

    private async void ButtonRefresh_Click(object sender, EventArgs e)
    {
        await RefreshQrCodeAsync();
    }

    private async Task RefreshQrCodeAsync()
    {
        try
        {
            labelStatus.Text = "Статус: Създаване на сесия...";
            buttonRefresh.Enabled = false;

            // Получаване на текущия Windows потребител от клиента
            string username = Environment.UserName;
            string domain = Environment.UserDomainName;
            
            // Опит за получаване на по-точна информация
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                if (identity != null && !string.IsNullOrEmpty(identity.Name))
                {
                    var parts = identity.Name.Split('\\');
                    if (parts.Length == 2)
                    {
                        domain = parts[0];
                        username = parts[1];
                    }
                }
            }
            catch
            {
                // Ако не успее, използваме Environment
            }
            
            _logger.LogInfo($"Създаване на сесия с потребител: {username}@{domain}");

            _currentSession = await _apiClient.CreateSessionAsync(username, domain);

            if (_currentSession != null)
            {
                // Генериране на URL за мобилното приложение
                string qrData = $"{_apiClient.BaseAddress}/auth?token={_currentSession.AccessToken}";
                
                _logger.LogInfo($"Генериране на QR код с данни: {qrData}");
                
                Bitmap qrBitmap = _qrCodeService.GenerateQrCode(qrData, 300);
                pictureBoxQrCode.Image?.Dispose();
                pictureBoxQrCode.Image = qrBitmap;

                labelMachineName.Text = $"Машина: {_currentSession.MachineName}";
                labelUsername.Text = $"Потребител: {_currentSession.WindowsUsername}@{_currentSession.Domain}";
                labelStatus.Text = "Статус: Очакване на сканиране";
                labelStatus.ForeColor = Color.Orange;

                // Обновяване на lock screen формата ако съществува
                if (_lockScreenForm != null && !_lockScreenForm.IsDisposed)
                {
                    _lockScreenForm.UpdateQrCode(qrData);
                }

                _logger.LogInfo($"Създадена нова сесия: {_currentSession.SessionId}, Token: {_currentSession.AccessToken}");
                _logger.LogInfo($"QR код URL: {qrData}");
                
                // Уверете се, че timer-ът работи
                if (!timerStatusCheck.Enabled)
                {
                    timerStatusCheck.Start();
                    _logger.LogInfo("Timer за проверка на статус е стартиран");
                }
            }
            else
            {
                labelStatus.Text = "Статус: Грешка при създаване на сесия";
                labelStatus.ForeColor = Color.Red;
                _logger.LogError("Неуспешно създаване на сесия");
                
                // Показване на placeholder изображение или съобщение
                pictureBoxQrCode.Image?.Dispose();
                pictureBoxQrCode.Image = null;
                
                // Показване на съобщение за грешка с текущия API URL
                string apiUrl = _apiClient.BaseAddress;
                MessageBox.Show(
                    $"Неуспешно създаване на сесия!\n\n" +
                    $"API URL: {apiUrl}\n\n" +
                    "Провери:\n" +
                    "1. Дали API-то е достъпно на този адрес\n" +
                    "2. Дали има интернет връзка\n" +
                    "3. Дали API-то е стартирано\n" +
                    "4. Провери логовете в LOGS папката\n\n" +
                    $"Моля, конфигурирай appsettings.json с правилния BaseUrl",
                    "Грешка - Няма връзка с API",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            string errorMessage = ex.Message;
            if (ex.InnerException != null)
            {
                errorMessage += $" ({ex.InnerException.Message})";
            }
            
            labelStatus.Text = $"Статус: Грешка - {errorMessage}";
            labelStatus.ForeColor = Color.Red;
            _logger.LogError($"Грешка при обновяване на QR код: {errorMessage}", ex);
            
            // Показване на по-подробно съобщение за грешка
            string apiUrl = _apiClient.BaseAddress;
            MessageBox.Show(
                $"Грешка при свързване с API!\n\n" +
                $"Тип грешка: {ex.GetType().Name}\n" +
                $"Съобщение: {errorMessage}\n\n" +
                $"API URL: {apiUrl}\n\n" +
                "Възможни причини:\n" +
                "1. API-то не е стартирано\n" +
                "2. Неправилен URL адрес\n" +
                "3. Мрежова грешка или firewall блокира\n" +
                "4. SSL сертификат проблем\n\n" +
                "Провери логовете в LOGS папката за подробности.",
                "Грешка при свързване",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            
            // Показване на грешката
            pictureBoxQrCode.Image?.Dispose();
            pictureBoxQrCode.Image = null;
            
            MessageBox.Show(
                $"Грешка при обновяване на QR код:\n\n{ex.Message}\n\n" +
                $"Детайли: {ex.GetType().Name}\n\n" +
                "Провери логовете в LOGS папката за повече информация.",
                "Грешка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            buttonRefresh.Enabled = true;
        }
    }

    private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<object, SessionSwitchEventArgs>(SystemEvents_SessionSwitch), sender, e);
            return;
        }

        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
                // Екранът е заключен - показваме lock screen формата
                ShowLockScreenQr();
                _logger.LogInfo("Екранът е заключен - показване на QR код");
                break;

            case SessionSwitchReason.SessionUnlock:
                // Екранът е отключен - скриваме lock screen формата
                HideLockScreenQr();
                _logger.LogInfo("Екранът е отключен - скриване на QR код");
                break;
        }
    }

    private void ShowLockScreenQr()
    {
        if (_lockScreenForm == null || _lockScreenForm.IsDisposed)
        {
            _lockScreenForm = new LockScreenQrForm(_qrCodeService);
            
            if (_currentSession != null)
            {
                string qrData = $"{_apiClient.BaseAddress}/auth?token={_currentSession.AccessToken}";
                _lockScreenForm.UpdateQrCode(qrData);
            }
            
            _lockScreenForm.Show();
        }
    }

    private void HideLockScreenQr()
    {
        if (_lockScreenForm != null && !_lockScreenForm.IsDisposed)
        {
            _lockScreenForm.Hide();
            _lockScreenForm.Dispose();
            _lockScreenForm = null;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
        if (_lockScreenForm != null && !_lockScreenForm.IsDisposed)
        {
            _lockScreenForm.Close();
            _lockScreenForm.Dispose();
        }
        
        base.OnFormClosing(e);
    }

    private async void TimerStatusCheck_Tick(object sender, EventArgs e)
    {
        if (_currentSession != null)
        {
            try
            {
                _logger.LogInfo($"⏰ [TIMER] Проверка на статус за сесия: {_currentSession.SessionId}");
                SessionStatus? status = await _apiClient.GetSessionStatusAsync(_currentSession.SessionId);

                if (status.HasValue)
                {
                    _logger.LogInfo($"✅ [TIMER] Статус на сесия {_currentSession.SessionId}: {status.Value}");
                    
                    // Обновяване на UI в UI thread
                    if (InvokeRequired)
                    {
                        Invoke(new Action(() => UpdateStatusUI(status.Value)));
                    }
                    else
                    {
                        UpdateStatusUI(status.Value);
                    }
                }
                else
                {
                    _logger.LogWarning($"⚠️ [TIMER] Неуспешно получаване на статус за сесия {_currentSession.SessionId} (null)");
                    // Обновяване на UI за да покажем че все още чакаме
                    if (InvokeRequired)
                    {
                        Invoke(new Action(() => {
                            labelStatus.Text = "Статус: Очакване... (няма отговор)";
                            labelStatus.ForeColor = Color.White;
                            panelStatus.BackColor = Color.FromArgb(251, 191, 36);
                        }));
                    }
                    else
                    {
                        labelStatus.Text = "Статус: Очакване... (няма отговор)";
                        labelStatus.ForeColor = Color.White;
                        panelStatus.BackColor = Color.FromArgb(251, 191, 36);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ [TIMER] Грешка при проверка на статус: {ex.Message}", ex);
            }
        }
        else
        {
            timerStatusCheck.Stop();
            _logger.LogWarning("⚠️ [TIMER] Няма активна сесия - таймерът е спрян");
        }
    }

    private async void UpdateStatusUI(SessionStatus status)
    {
        switch (status)
        {
            case SessionStatus.Approved:
                labelStatus.Text = "Статус: Одобрено ✓";
                labelStatus.ForeColor = Color.White;
                panelStatus.BackColor = Color.FromArgb(34, 197, 94);
                timerStatusCheck.Stop();
                _logger.LogInfo($"🎉 [UI] Сесия {_currentSession?.SessionId} е одобрена - показване на съобщение");
                MessageBox.Show("Аутентикацията е успешна! Достъпът е разрешен.", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                // Създаване на нова сесия след одобрение
                await RefreshQrCodeAsync();
                timerStatusCheck.Start();
                break;
            case SessionStatus.Rejected:
                labelStatus.Text = "Статус: Отхвърлено ✗";
                labelStatus.ForeColor = Color.White;
                panelStatus.BackColor = Color.FromArgb(239, 68, 68);
                timerStatusCheck.Stop();
                _logger.LogWarning($"❌ [UI] Сесия {_currentSession?.SessionId} е отхвърлена - показване на съобщение");
                MessageBox.Show("Аутентикацията е отхвърлена.", "Отхвърлено", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                // Създаване на нова сесия след отхвърляне
                await RefreshQrCodeAsync();
                timerStatusCheck.Start();
                break;
            case SessionStatus.Expired:
                labelStatus.Text = "Статус: Изтекла";
                labelStatus.ForeColor = Color.FromArgb(156, 163, 175);
                panelStatus.BackColor = Color.FromArgb(156, 163, 175);
                timerStatusCheck.Stop();
                _logger.LogWarning($"⏰ [UI] Сесия {_currentSession?.SessionId} е изтекла - създаване на нова");
                // Създаване на нова сесия след изтичане
                await RefreshQrCodeAsync();
                timerStatusCheck.Start();
                break;
            case SessionStatus.Pending:
                // Продължаваме да чакаме
                labelStatus.Text = $"Статус: Очакване на сканиране... ({DateTime.Now:HH:mm:ss})";
                labelStatus.ForeColor = Color.White;
                panelStatus.BackColor = Color.FromArgb(251, 191, 36);
                break;
        }
    }

    private async void ButtonInstallCredentialProvider_Click(object sender, EventArgs e)
    {
        buttonInstallCredentialProvider.Enabled = false;
        labelStatus.Text = "Статус: Инсталиране на Credential Provider...";
        labelStatus.ForeColor = Color.Blue;

        try
        {
            // Ако не е admin – рестартираме с UAC elevation
            if (!_installerService.IsRunningAsAdministrator())
            {
                buttonInstallCredentialProvider.Enabled = true;
                ElevateAndExit();
                return;
            }

            // Проверка дали вече е инсталиран
            bool isInstalled = _installerService.IsInstalled();
            bool hasNewerVersion = _installerService.HasNewerVersion();
            
            if (isInstalled)
            {
                if (hasNewerVersion)
                {
                    DialogResult updateResult = MessageBox.Show(
                        "Credential Provider вече е инсталиран.\n\n" +
                        "Намерена е по-нова версия!\n\n" +
                        "Искаш ли да го обновиш?\n\n" +
                        "След обновяване ще трябва да рестартираш компютъра.",
                        "Обновяване на Credential Provider",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    
                    if (updateResult != DialogResult.Yes)
                    {
                        labelStatus.Text = "Статус: Обновяването е отменено";
                        labelStatus.ForeColor = Color.Orange;
                        buttonInstallCredentialProvider.Enabled = true;
                        return;
                    }
                }
                else
                {
                    DialogResult reinstallResult = MessageBox.Show(
                        "Credential Provider вече е инсталиран и е актуален.\n\n" +
                        "Искаш ли да го преинсталираш въпреки това?",
                        "Credential Provider вече е инсталиран",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    
                    if (reinstallResult != DialogResult.Yes)
                    {
                        labelStatus.Text = "Статус: Инсталацията е отменена";
                        labelStatus.ForeColor = Color.Orange;
                        buttonInstallCredentialProvider.Enabled = true;
                        return;
                    }
                }
            }

            // Показване на диалог за избор на файл ако не е намерен автоматично
            string? dllPath = _installerService.FindDllFile();
            if (string.IsNullOrEmpty(dllPath))
            {
                // Проверка дали сме на development машина
                bool isDevMachine = _installerService.IsDevelopmentMachine();
                string solutionPath = Path.GetFullPath(Path.Combine(Application.StartupPath, "..", "..", ".."));
                string expectedDllPath = Path.Combine(solutionPath, "bin", "x64", "Release", "ADS.WindowsAuth.CredentialProvider.dll");
                
                DialogResult rebuildResult;
                
                if (isDevMachine)
                {
                    // Development машина - предлагаме автоматичен rebuild
                    rebuildResult = MessageBox.Show(
                        $"DLL файлът не е намерен автоматично!\n\n" +
                        $"Очаквано местоположение:\n{expectedDllPath}\n\n" +
                        $"Искаш ли да rebuild-нем проекта автоматично?\n\n" +
                        $"(Ако избереш 'Не', можеш да избереш DLL файл ръчно)",
                        "Автоматичен rebuild?",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);
                }
                else
                {
                    // Production машина - само ръчен избор
                    rebuildResult = MessageBox.Show(
                        $"DLL файлът не е намерен автоматично!\n\n" +
                        $"Това изглежда е production машина (няма Visual Studio/source код).\n\n" +
                        $"Моля, избери DLL файла ръчно или го копирай в папката на приложението.\n\n" +
                        $"Искаш ли да избереш DLL файл ръчно?",
                        "DLL не е намерен",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    
                    if (rebuildResult == DialogResult.Yes)
                    {
                        rebuildResult = DialogResult.No; // Преобразуваме в "Не" за да влезе в ръчния избор
                    }
                    else
                    {
                        rebuildResult = DialogResult.Cancel; // Отказ
                    }
                }
                
                if (rebuildResult == DialogResult.Yes)
                {
                    // Автоматичен rebuild
                    labelStatus.Text = "Статус: Rebuild на проекта...";
                    labelStatus.ForeColor = Color.Blue;
                    Application.DoEvents();
                    
                    var buildResult = await _installerService.RebuildProjectAsync();
                    
                    if (buildResult.Success)
                    {
                        // Опит за намиране на DLL след rebuild
                        dllPath = _installerService.FindDllFile();
                        if (string.IsNullOrEmpty(dllPath))
                        {
                            MessageBox.Show(
                                $"Rebuild успешен, но DLL все още не е намерен!\n\n" +
                                $"Очаквано местоположение:\n{expectedDllPath}\n\n" +
                                $"Моля, провери дали проектът е компилиран правилно.",
                                "DLL не е намерен",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                            labelStatus.Text = "Статус: DLL не е намерен след rebuild";
                            labelStatus.ForeColor = Color.Orange;
                            buttonInstallCredentialProvider.Enabled = true;
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show(
                            $"Rebuild неуспешен:\n\n{buildResult.Message}\n\n" +
                            $"Моля, компилирай проекта ръчно в Visual Studio.",
                            "Rebuild неуспешен",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        labelStatus.Text = "Статус: Rebuild неуспешен";
                        labelStatus.ForeColor = Color.Red;
                        buttonInstallCredentialProvider.Enabled = true;
                        return;
                    }
                }
                else if (rebuildResult == DialogResult.No)
                {
                    // Ръчен избор на файл
                    using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Filter = "DLL Files (*.dll)|*.dll|All Files (*.*)|*.*";
                    dialog.Title = "Избери Credential Provider DLL файл";
                    dialog.FileName = "ADS.WindowsAuth.CredentialProvider.dll";
                        dialog.InitialDirectory = solutionPath;
                    
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        dllPath = dialog.FileName;
                    }
                    else
                    {
                        labelStatus.Text = "Статус: Инсталацията е отменена";
                        labelStatus.ForeColor = Color.Orange;
                        buttonInstallCredentialProvider.Enabled = true;
                        return;
                    }
                }
                }
                else // Cancel
                {
                    labelStatus.Text = "Статус: Инсталацията е отменена";
                    labelStatus.ForeColor = Color.Orange;
                    buttonInstallCredentialProvider.Enabled = true;
                    return;
                }
            }
            else
            {
                // Показване на информация кой DLL ще се инсталира + опция за rebuild (само на dev машина)
                _logger.LogInfo($"DLL намерен: {dllPath}");
                FileInfo dllInfo = new FileInfo(dllPath);
                
                bool isDevMachine = _installerService.IsDevelopmentMachine();
                DialogResult rebuildOption;
                
                if (isDevMachine)
                {
                    // Development машина - предлагаме rebuild опция
                    rebuildOption = MessageBox.Show(
                        $"DLL файл намерен:\n\n" +
                        $"Път: {dllPath}\n" +
                        $"Размер: {dllInfo.Length / 1024} KB\n" +
                        $"Дата: {dllInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}\n\n" +
                        $"Искаш ли да rebuild-нем проекта преди инсталация?\n\n" +
                        $"(Да = Rebuild и инсталирай, Не = Инсталирай сегашния DLL, Отказ = Отмени)",
                        "Rebuild преди инсталация?",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);
                }
                else
                {
                    // Production машина - само потвърждение за инсталация
                    rebuildOption = MessageBox.Show(
                        $"DLL файл намерен:\n\n" +
                        $"Път: {dllPath}\n" +
                        $"Размер: {dllInfo.Length / 1024} KB\n" +
                        $"Дата: {dllInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}\n\n" +
                        $"След инсталация ще трябва да рестартираш компютъра!\n\n" +
                        $"Искаш ли да продължиш с инсталацията?",
                        "Потвърждение на инсталация",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    
                    if (rebuildOption == DialogResult.Yes)
                    {
                        rebuildOption = DialogResult.No; // Преобразуваме в "Не" за да продължи с инсталацията
                    }
                    else
                    {
                        rebuildOption = DialogResult.Cancel; // Отказ
                    }
                }
                
                if (rebuildOption == DialogResult.Cancel)
                {
                    labelStatus.Text = "Статус: Инсталацията е отменена";
                    labelStatus.ForeColor = Color.Orange;
                    buttonInstallCredentialProvider.Enabled = true;
                    return;
                }
                else if (rebuildOption == DialogResult.Yes)
                {
                    // Rebuild преди инсталация
                    labelStatus.Text = "Статус: Rebuild на проекта...";
                    labelStatus.ForeColor = Color.Blue;
                    Application.DoEvents();
                    
                    var buildResult = await _installerService.RebuildProjectAsync();
                    
                    if (buildResult.Success)
                    {
                        // Опит за намиране на новия DLL
                        string? newDllPath = _installerService.FindDllFile();
                        if (!string.IsNullOrEmpty(newDllPath))
                        {
                            dllPath = newDllPath;
                            FileInfo newDllInfo = new FileInfo(dllPath);
                            _logger.LogInfo($"Нов DLL намерен след rebuild: {dllPath} (Дата: {newDllInfo.LastWriteTime})");
                        }
                        else
                        {
                            _logger.LogWarning("Rebuild успешен, но новият DLL не е намерен. Използвам стария.");
                        }
                    }
                    else
                    {
                        DialogResult continueResult = MessageBox.Show(
                            $"Rebuild неуспешен:\n\n{buildResult.Message}\n\n" +
                            $"Искаш ли да продължиш с инсталацията на сегашния DLL?",
                            "Rebuild неуспешен",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);
                        
                        if (continueResult == DialogResult.No)
                        {
                            labelStatus.Text = "Статус: Инсталацията е отменена";
                            labelStatus.ForeColor = Color.Orange;
                            buttonInstallCredentialProvider.Enabled = true;
                            return;
                        }
                        // Продължаваме със стария DLL
                    }
                }
                // Ако е "Не", продължаваме директно с инсталацията
            }

            // Инсталация/Обновяване
            InstallResult installResult = await _installerService.InstallAsync(dllPath, forceUpdate: true);

            if (installResult.Success)
            {
                MessageBox.Show(
                    installResult.Message + "\n\nСлед рестарт на компютъра, QR кодът ще се появи на login екрана.",
                    "Инсталация успешна",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                labelStatus.Text = "Статус: Инсталация успешна!";
                labelStatus.ForeColor = Color.Green;
            }
            else
            {
                MessageBox.Show(
                    installResult.Message,
                    "Грешка при инсталация",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                labelStatus.Text = "Статус: Грешка при инсталация";
                labelStatus.ForeColor = Color.Red;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при инсталация на Credential Provider", ex);
            MessageBox.Show(
                $"Грешка при инсталация: {ex.Message}",
                "Грешка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            labelStatus.Text = "Статус: Грешка";
            labelStatus.ForeColor = Color.Red;
        }
        finally
        {
            buttonInstallCredentialProvider.Enabled = true;
        }
    }

    private async void ButtonInstallMonitor_Click(object sender, EventArgs e)
    {
        buttonInstallMonitor.Enabled = false;
        labelStatus.Text = "Статус: Инсталиране на Monitor Service...";
        labelStatus.ForeColor = Color.Blue;

        try
        {
            // Ако не е admin – рестартираме с UAC elevation
            if (!_monitorInstallerService.IsRunningAsAdministrator())
            {
                buttonInstallMonitor.Enabled = true;
                ElevateAndExit();
                return;
            }

            // Проверка дали вече е инсталиран
            bool isInstalled = _monitorInstallerService.IsServiceInstalled();
            string actionText = isInstalled ? "обновяване" : "инсталиране";
            
            if (isInstalled)
            {
                DialogResult updateResult = MessageBox.Show(
                    "Monitor Service вече е инсталиран.\n\n" +
                    "Искаш ли да го обновиш с нова версия?\n\n" +
                    "Сервизът ще бъде спрян временно по време на обновяването.",
                    "Обновяване на Monitor Service",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                
                if (updateResult != DialogResult.Yes)
                {
                    labelStatus.Text = "Статус: Обновяването е отменено";
                    labelStatus.ForeColor = Color.Orange;
                    buttonInstallMonitor.Enabled = true;
                    return;
                }
            }

            // Показване на диалог за избор на файл ако не е намерен автоматично
            string? exePath = _monitorInstallerService.FindMonitorExe();
            if (string.IsNullOrEmpty(exePath))
            {
                // Показване на информация къде да се намери Monitor EXE
                string solutionPath = Path.GetFullPath(Path.Combine(Application.StartupPath, "..", "..", ".."));
                string expectedExePath = Path.Combine(solutionPath, "ADS.WindowsAuth.Monitor", "bin", "Release", "net8.0-windows8.0", "ADS.WindowsAuth.Monitor.exe");
                
                DialogResult infoResult = MessageBox.Show(
                    $"Monitor EXE файлът не е намерен автоматично!\n\n" +
                    $"Очаквано местоположение:\n{expectedExePath}\n\n" +
                    $"За да компилираш Monitor Service:\n" +
                    $"1. Отвори Visual Studio\n" +
                    $"2. Отвори solution файла\n" +
                    $"3. Избери Configuration: Release, Platform: Any CPU\n" +
                    $"4. Build -> Rebuild Solution\n" +
                    $"5. EXE-то ще се намери в: ADS.WindowsAuth.Monitor\\bin\\Release\\net8.0-windows8.0\\\n\n" +
                    $"Искаш ли да избереш EXE файл ръчно?",
                    "Monitor EXE не е намерен",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                
                if (infoResult == DialogResult.No)
                {
                    labelStatus.Text = "Статус: Инсталацията е отменена";
                    labelStatus.ForeColor = Color.Orange;
                    buttonInstallMonitor.Enabled = true;
                    return;
                }
                
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Filter = "EXE Files (*.exe)|*.exe|All Files (*.*)|*.*";
                    dialog.Title = "Избери Monitor Service EXE файл";
                    dialog.FileName = "ADS.WindowsAuth.Monitor.exe";
                    dialog.InitialDirectory = solutionPath;
                    
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        exePath = dialog.FileName;
                    }
                    else
                    {
                        labelStatus.Text = "Статус: Инсталацията е отменена";
                        labelStatus.ForeColor = Color.Orange;
                        buttonInstallMonitor.Enabled = true;
                        return;
                    }
                }
            }
            else
            {
                // Показване на информация кой EXE ще се инсталира
                _logger.LogInfo($"Ще се инсталира Monitor EXE от: {exePath}");
                FileInfo exeInfo = new FileInfo(exePath);
                MessageBox.Show(
                    $"Ще се инсталира Monitor Service:\n\n" +
                    $"Път: {exePath}\n" +
                    $"Размер: {exeInfo.Length / 1024} KB\n" +
                    $"Дата: {exeInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}\n\n" +
                    $"След инсталация Monitor Service ще работи като Windows Service!",
                    "Потвърждение на инсталация",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            // Инсталация/Обновяване
            InstallResult monitorResult = await _monitorInstallerService.InstallAsync(exePath);

            if (monitorResult.Success)
            {
                MessageBox.Show(
                    monitorResult.Message,
                    isInstalled ? "Обновяване успешно" : "Инсталация успешна",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                labelStatus.Text = isInstalled ? "Статус: Обновяване успешно!" : "Статус: Инсталация успешна!";
                labelStatus.ForeColor = Color.Green;
            }
            else
            {
                MessageBox.Show(
                    monitorResult.Message,
                    "Грешка при инсталация",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                labelStatus.Text = "Статус: Грешка при инсталация";
                labelStatus.ForeColor = Color.Red;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при инсталация на Monitor Service", ex);
            MessageBox.Show(
                $"Грешка при инсталация: {ex.Message}",
                "Грешка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            labelStatus.Text = "Статус: Грешка";
            labelStatus.ForeColor = Color.Red;
        }
        finally
        {
            buttonInstallMonitor.Enabled = true;
        }
    }

    private async void ButtonInstallRemoteDesktop_Click(object sender, EventArgs e)
    {
        buttonInstallRemoteDesktop.Enabled = false;
        labelStatus.Text = "Статус: Инсталиране на Remote Desktop Host...";
        labelStatus.ForeColor = Color.Blue;

        try
        {
            if (!_rdInstallerService.IsRunningAsAdministrator())
            {
                buttonInstallRemoteDesktop.Enabled = true;
                ElevateAndExit();
                return;
            }

            bool isInstalled = _rdInstallerService.IsInstalled();
            if (isInstalled)
            {
                var answer = MessageBox.Show(
                    "RemoteDesktopHost вече е инсталиран.\n\nИскаш ли да го обновиш?",
                    "Обновяване",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (answer != DialogResult.Yes)
                {
                    labelStatus.Text = "Статус: Отменено";
                    labelStatus.ForeColor = Color.Orange;
                    buttonInstallRemoteDesktop.Enabled = true;
                    return;
                }
            }

            string? exePath = _rdInstallerService.FindExe();
            if (string.IsNullOrEmpty(exePath))
            {
                using var dialog = new OpenFileDialog
                {
                    Filter = "EXE Files (*.exe)|*.exe",
                    Title = "Избери ADS.WindowsAuth.RemoteDesktopHost.exe",
                    FileName = "ADS.WindowsAuth.RemoteDesktopHost.exe"
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                    exePath = dialog.FileName;
                else
                {
                    labelStatus.Text = "Статус: Отменено";
                    labelStatus.ForeColor = Color.Orange;
                    buttonInstallRemoteDesktop.Enabled = true;
                    return;
                }
            }

            InstallResult result = await _rdInstallerService.InstallAsync(exePath);

            if (result.Success)
            {
                MessageBox.Show(result.Message, "Инсталация успешна", MessageBoxButtons.OK, MessageBoxIcon.Information);
                labelStatus.Text = "Статус: Remote Desktop инсталиран!";
                labelStatus.ForeColor = Color.Green;
            }
            else
            {
                MessageBox.Show(result.Message, "Грешка при инсталация", MessageBoxButtons.OK, MessageBoxIcon.Error);
                labelStatus.Text = "Статус: Грешка при инсталация";
                labelStatus.ForeColor = Color.Red;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при инсталация на RemoteDesktopHost", ex);
            MessageBox.Show($"Грешка: {ex.Message}", "Грешка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            labelStatus.Text = "Статус: Грешка";
            labelStatus.ForeColor = Color.Red;
        }
        finally
        {
            buttonInstallRemoteDesktop.Enabled = true;
        }
    }

    /// <summary>
    /// Ако приложението не е стартирано като Administrator, рестартира го с UAC elevation (runas).
    /// Текущата инстанция се затваря.
    /// </summary>
    private static void ElevateAndExit()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = Application.ExecutablePath,
                UseShellExecute = true,
                Verb = "runas"   // UAC диалог
            };
            System.Diagnostics.Process.Start(psi);
            Application.Exit();
        }
        catch
        {
            // Потребителят е натиснал "Не" на UAC диалога
            MessageBox.Show(
                "Трябва да одобриш административните права за инсталация.",
                "Нужни са администраторски права",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
