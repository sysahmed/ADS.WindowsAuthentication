using ADS.WindowsAuth.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ADS.WindowsAuth.API.Controllers;

/// <summary>
/// Администраторски контролер за управление на блокирани IP адреси.
/// Достъпен само за потребители с роля Admin.
/// </summary>
[Authorize(Roles = "Admin")]
public class SecurityController : Controller
{
    private readonly BruteForceProtectionService _bruteForce;
    private readonly ILogger<SecurityController> _logger;

    public SecurityController(
        BruteForceProtectionService bruteForce,
        ILogger<SecurityController> logger)
    {
        _bruteForce = bruteForce;
        _logger = logger;
    }

    /// <summary>Страница за управление на блокирани IP адреси.</summary>
    [HttpGet("/security/blocked-ips")]
    public async Task<IActionResult> BlockedIps()
    {
        var blockedIps = await _bruteForce.GetBlockedIpsAsync();
        return View(blockedIps);
    }

    /// <summary>Деблокиране на IP адрес (POST).</summary>
    [HttpPost("/security/blocked-ips/unblock")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnblockIp(string ip, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            TempData["Error"] = "Невалиден IP адрес.";
            return RedirectToAction("BlockedIps");
        }

        var adminUsername = User.Identity?.Name ?? "unknown";
        bool success = await _bruteForce.UnblockIpAsync(ip, adminUsername, reason);

        if (success)
        {
            _logger.LogInformation("Admin {Admin} деблокира IP {Ip}. Причина: {Reason}",
                adminUsername, ip, reason ?? "не е посочена");
            TempData["Success"] = $"IP адресът {ip} е деблокиран успешно.";
        }
        else
        {
            TempData["Error"] = $"IP адресът {ip} не е намерен в списъка с блокирани адреси.";
        }

        return RedirectToAction("BlockedIps");
    }

    // ── API endpoints (за AJAX) ───────────────────────────────────────────

    /// <summary>Списък с блокирани IP адреси (JSON).</summary>
    [HttpGet("api/security/blocked-ips")]
    public async Task<IActionResult> GetBlockedIps()
    {
        var list = await _bruteForce.GetBlockedIpsAsync();
        return Ok(list.Select(b => new
        {
            b.Id,
            b.IpAddress,
            b.FailedAttempts,
            b.BlockedAt,
            b.LastAttemptAt,
            b.UnblockedAt,
            b.UnblockedBy,
            b.UnblockReason,
            b.IsBlocked
        }));
    }

    /// <summary>Деблокиране на IP (JSON API).</summary>
    [HttpPost("api/security/blocked-ips/{ip}/unblock")]
    public async Task<IActionResult> UnblockIpApi(string ip, [FromBody] UnblockRequest? req = null)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return BadRequest(new { message = "Невалиден IP адрес." });

        var adminUsername = User.Identity?.Name ?? "unknown";
        bool success = await _bruteForce.UnblockIpAsync(ip, adminUsername, req?.Reason);

        if (!success)
            return NotFound(new { message = $"IP {ip} не е намерен или вече е деблокиран." });

        return Ok(new { message = $"IP {ip} е деблокиран успешно." });
    }
}

public class UnblockRequest
{
    public string? Reason { get; set; }
}
