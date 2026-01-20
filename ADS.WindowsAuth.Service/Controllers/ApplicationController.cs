using Microsoft.AspNetCore.Mvc;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;
using System.Diagnostics;

namespace ADS.WindowsAuth.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApplicationController : ControllerBase
{
    private readonly ILoggerService _logger;
    private static readonly Dictionary<int, ApplicationInstallation> _installations = new();
    private static int _nextInstallationId = 1;

    public ApplicationController(ILoggerService logger)
    {
        _logger = logger;
    }

    [HttpPost("install")]
    public IActionResult InstallApplication([FromBody] ApplicationInstallationRequest request)
    {
        ApplicationInstallation installation = new ApplicationInstallation
        {
            Id = Interlocked.Increment(ref _nextInstallationId),
            ApplicationName = request.ApplicationName,
            InstallerPath = request.InstallerPath,
            InstallerType = request.InstallerType,
            InstallParameters = request.InstallParameters ?? "",
            TargetMachine = request.TargetMachine,
            Status = InstallationStatus.Pending,
            RequestedAt = DateTime.Now,
            RequestedBy = request.RequestedBy
        };

        _installations.TryAdd(installation.Id, installation);

        // Стартиране на инсталацията асинхронно
        _ = Task.Run(() => ExecuteInstallation(installation));

        _logger.LogInfo($"Заявка за инсталация на {request.ApplicationName} на {request.TargetMachine}");

        return Ok(installation);
    }

    [HttpGet("installations")]
    public IActionResult GetAllInstallations()
    {
        return Ok(_installations.Values.ToList());
    }

    [HttpGet("installations/{id}")]
    public IActionResult GetInstallation(int id)
    {
        if (_installations.TryGetValue(id, out ApplicationInstallation? installation))
        {
            return Ok(installation);
        }

        return NotFound(new { message = "Инсталацията не е намерена" });
    }

    [HttpPost("uninstall")]
    public IActionResult UninstallApplication([FromBody] UninstallApplicationRequest request)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                Arguments = $"/x {request.ProductCode} /qn",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process process = Process.Start(startInfo);
            process?.WaitForExit();

            _logger.LogInfo($"Деинсталирано приложение: {request.ApplicationName}");

            return Ok(new { message = "Деинсталацията е започната" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при деинсталация на {request.ApplicationName}", ex);
            return StatusCode(500, new { message = "Грешка при деинсталация" });
        }
    }

    [HttpGet("installed")]
    public IActionResult GetInstalledApplications([FromQuery] string? machineName)
    {
        try
        {
            List<InstalledApplication> installedApps = new List<InstalledApplication>();

            // Четене на инсталирани приложения от Registry
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
            {
                if (key != null)
                {
                    foreach (string subkeyName in key.GetSubKeyNames())
                    {
                        using (Microsoft.Win32.RegistryKey subkey = key.OpenSubKey(subkeyName))
                        {
                            if (subkey != null)
                            {
                                string? displayName = subkey.GetValue("DisplayName")?.ToString();
                                string? publisher = subkey.GetValue("Publisher")?.ToString();
                                string? version = subkey.GetValue("DisplayVersion")?.ToString();
                                string? installDate = subkey.GetValue("InstallDate")?.ToString();

                                if (!string.IsNullOrEmpty(displayName))
                                {
                                    installedApps.Add(new InstalledApplication
                                    {
                                        Name = displayName,
                                        Publisher = publisher ?? "",
                                        Version = version ?? "",
                                        InstallDate = installDate ?? "",
                                        MachineName = machineName ?? Environment.MachineName
                                    });
                                }
                            }
                        }
                    }
                }
            }

            return Ok(installedApps);
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при получаване на инсталирани приложения", ex);
            return StatusCode(500, new { message = "Грешка при получаване на приложения" });
        }
    }

    private void ExecuteInstallation(ApplicationInstallation installation)
    {
        installation.Status = InstallationStatus.InProgress;
        installation.StatusMessage = "Инсталацията е започнала";

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();

            switch (installation.InstallerType.ToUpper())
            {
                case "MSI":
                    startInfo.FileName = "msiexec.exe";
                    startInfo.Arguments = $"/i \"{installation.InstallerPath}\" {installation.InstallParameters} /qn";
                    break;

                case "EXE":
                    startInfo.FileName = installation.InstallerPath;
                    startInfo.Arguments = installation.InstallParameters;
                    break;

                default:
                    throw new NotSupportedException($"Типът инсталатор {installation.InstallerType} не се поддържа");
            }

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;

            Process process = Process.Start(startInfo);
            process?.WaitForExit();

            if (process?.ExitCode == 0)
            {
                installation.Status = InstallationStatus.Completed;
                installation.StatusMessage = "Инсталацията е завършена успешно";
                installation.CompletedAt = DateTime.Now;
                _logger.LogInfo($"Успешна инсталация на {installation.ApplicationName}");
            }
            else
            {
                installation.Status = InstallationStatus.Failed;
                installation.StatusMessage = $"Инсталацията е неуспешна. Exit code: {process?.ExitCode}";
                installation.CompletedAt = DateTime.Now;
                _logger.LogError($"Неуспешна инсталация на {installation.ApplicationName}. Exit code: {process?.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            installation.Status = InstallationStatus.Failed;
            installation.StatusMessage = $"Грешка: {ex.Message}";
            installation.CompletedAt = DateTime.Now;
            _logger.LogError($"Грешка при инсталация на {installation.ApplicationName}", ex);
        }
    }
}

public class ApplicationInstallationRequest
{
    public string ApplicationName { get; set; } = string.Empty;
    public string InstallerPath { get; set; } = string.Empty;
    public string InstallerType { get; set; } = string.Empty; // MSI, EXE, URL
    public string? InstallParameters { get; set; }
    public string TargetMachine { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
}

public class UninstallApplicationRequest
{
    public string ApplicationName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
}

public class InstalledApplication
{
    public string Name { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string InstallDate { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
}

