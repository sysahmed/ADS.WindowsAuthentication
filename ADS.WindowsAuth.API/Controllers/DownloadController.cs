using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADS.WindowsAuth.API.Controllers;

/// <summary>
/// Контролер за изтегляне на Client и Installer
/// </summary>
[AllowAnonymous]
[Route("download")]
public class DownloadController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DownloadController> _logger;

    public DownloadController(IWebHostEnvironment env, ILogger<DownloadController> logger)
    {
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// Изтегляне на Install-ADS.ps1 PowerShell скрипт
    /// </summary>
    [HttpGet("installer")]
    public IActionResult Installer()
    {
        var path = Path.Combine(_env.WebRootPath, "Install-ADS.ps1");
        if (!System.IO.File.Exists(path))
        {
            _logger.LogWarning("Install-ADS.ps1 не е намерен в wwwroot");
            return NotFound("Инсталационният скрипт не е наличен. Компилирайте API проекта.");
        }
        return PhysicalFile(path, "text/plain", "Install-ADS.ps1");
    }

    /// <summary>
    /// Изтегляне на ADS.WindowsAuth.Client.exe
    /// Търси в типични build локации
    /// </summary>
    [HttpGet("client")]
    public IActionResult Client()
    {
        var baseDir = _env.ContentRootPath;
        var possiblePaths = new[]
        {
            Path.Combine(baseDir, "..", "ADS.WindowsAuth.Client", "bin", "Release", "net8.0-windows", "ADS.WindowsAuth.Client.exe"),
            Path.Combine(baseDir, "..", "ADS.WindowsAuth.Client", "bin", "Debug", "net8.0-windows", "ADS.WindowsAuth.Client.exe"),
            Path.Combine(baseDir, "wwwroot", "ADS.WindowsAuth.Client.exe"),
            Path.Combine(_env.WebRootPath, "ADS.WindowsAuth.Client.exe"),
        };

        foreach (var p in possiblePaths)
        {
            var fullPath = Path.GetFullPath(p);
            if (System.IO.File.Exists(fullPath))
            {
                _logger.LogInformation("Client изтеглен от: {Path}", fullPath);
                return PhysicalFile(fullPath, "application/octet-stream", "ADS.WindowsAuth.Client.exe");
            }
        }

        _logger.LogWarning("ADS.WindowsAuth.Client.exe не е намерен");
        return NotFound("Клиентът не е наличен. Компилирайте ADS.WindowsAuth.Client и копирайте exe в wwwroot.");
    }
}
