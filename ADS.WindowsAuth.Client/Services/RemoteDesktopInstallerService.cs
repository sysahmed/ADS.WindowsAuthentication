using System.Diagnostics;
using System.Security.Principal;
using ADS.WindowsAuth.Core.Services;

namespace ADS.WindowsAuth.Client.Services;

/// <summary>
/// Инсталира RemoteDesktopHost в C:\ADS\RemoteDesktopHost.
/// Monitor Service го стартира автоматично в потребителската сесия чрез CreateProcessAsUser.
/// </summary>
public class RemoteDesktopInstallerService
{
    private readonly ILoggerService _logger;
    private const string INSTALL_PATH = @"C:\ADS\RemoteDesktopHost";
    private const string EXE_NAME = "ADS.WindowsAuth.RemoteDesktopHost.exe";

    public RemoteDesktopInstallerService(ILoggerService logger)
    {
        _logger = logger;
    }

    public bool IsRunningAsAdministrator()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    public bool IsInstalled() => File.Exists(Path.Combine(INSTALL_PATH, EXE_NAME));

    /// <summary>
    /// Търси RemoteDesktopHost.exe в стандартни локации.
    /// </summary>
    public string? FindExe()
    {
        var paths = new[]
        {
            // До клиента (ако са публикувани заедно)
            Path.Combine(Application.StartupPath, EXE_NAME),
            Path.Combine(Application.StartupPath, "RemoteDesktopHost", EXE_NAME),
            // Source tree (dev)
            Path.Combine(Application.StartupPath, "..", "..", "..", "ADS.WindowsAuth.RemoteDesktopHost", "bin", "Release", "net8.0-windows8.0", EXE_NAME),
            Path.Combine(Application.StartupPath, "..", "..", "..", "ADS.WindowsAuth.RemoteDesktopHost", "bin", "Debug",   "net8.0-windows8.0", EXE_NAME),
            // До Monitor (ако е инсталиран)
            Path.Combine(@"C:\ADS\Monitor", "RemoteDesktopHost", EXE_NAME),
            // Вече инсталиран
            Path.Combine(INSTALL_PATH, EXE_NAME),
        };

        foreach (var p in paths)
        {
            try
            {
                string full = Path.GetFullPath(p);
                if (File.Exists(full))
                {
                    _logger.LogInfo($"[RDH Installer] Намерен exe: {full}");
                    return full;
                }
            }
            catch { }
        }

        _logger.LogWarning("[RDH Installer] RemoteDesktopHost.exe не е намерен.");
        return null;
    }

    /// <summary>
    /// Инсталира RemoteDesktopHost в C:\ADS\RemoteDesktopHost.
    /// Monitor Service ще го стартира автоматично при следващия login.
    /// </summary>
    public async Task<InstallResult> InstallAsync(string? exePath = null)
    {
        try
        {
            if (!IsRunningAsAdministrator())
                return new InstallResult { Success = false, Message = "Нужни са администраторски права!" };

            exePath ??= FindExe();
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return new InstallResult
                {
                    Success = false,
                    Message = $"ADS.WindowsAuth.RemoteDesktopHost.exe не е намерен!\n\n" +
                              "Компилирай проекта в Release или копирай exe-то до клиента."
                };

            _logger.LogInfo($"[RDH Installer] Инсталиране от: {exePath}");

            if (!Directory.Exists(INSTALL_PATH))
                Directory.CreateDirectory(INSTALL_PATH);

            string targetExe = Path.Combine(INSTALL_PATH, EXE_NAME);
            string sourceDir  = Path.GetDirectoryName(exePath) ?? "";

            File.Copy(exePath, targetExe, overwrite: true);
            _logger.LogInfo("[RDH Installer] EXE копиран.");

            if (!string.IsNullOrEmpty(sourceDir))
            {
                foreach (var dll in Directory.GetFiles(sourceDir, "*.dll"))
                    File.Copy(dll, Path.Combine(INSTALL_PATH, Path.GetFileName(dll)), overwrite: true);

                foreach (var json in Directory.GetFiles(sourceDir, "*.json"))
                    File.Copy(json, Path.Combine(INSTALL_PATH, Path.GetFileName(json)), overwrite: true);

                string runtimesSrc = Path.Combine(sourceDir, "runtimes");
                if (Directory.Exists(runtimesSrc))
                {
                    CopyDirectory(runtimesSrc, Path.Combine(INSTALL_PATH, "runtimes"));
                    _logger.LogInfo("[RDH Installer] Папка runtimes копирана.");
                }
            }

            await Task.CompletedTask; // структурата е async за бъдещи нужди

            _logger.LogInfo("[RDH Installer] Инсталацията завърши успешно.");
            return new InstallResult
            {
                Success = true,
                Message = $"RemoteDesktopHost е инсталиран в:\n{INSTALL_PATH}\n\n" +
                          "Monitor Service ще го стартира автоматично в потребителската сесия при следващото стартиране."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("[RDH Installer] Грешка при инсталация", ex);
            return new InstallResult { Success = false, Message = $"Грешка при инсталация: {ex.Message}" };
        }
    }

    private static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(src))
            CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)));
    }
}
