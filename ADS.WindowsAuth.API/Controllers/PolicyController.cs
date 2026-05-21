using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;

namespace ADS.WindowsAuth.API.Controllers;

/// <summary>
/// Контролер за управление на политики
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PolicyController : ControllerBase
{
    private readonly IPolicyService _policyService;
    private readonly ILoggerService _logger;

    public PolicyController(IPolicyService policyService, ILoggerService logger)
    {
        _policyService = policyService;
        _logger = logger;
    }

    /// <summary>
    /// Получава всички политики
    /// </summary>
    [HttpGet]
    public IActionResult GetAllPolicies()
    {
        try
        {
            List<Policy> policies = _policyService.GetAllPolicies();
            return Ok(policies);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при получаване на политики: {ex.Message}", ex);
            return StatusCode(500, new { message = "Грешка при получаване на политики" });
        }
    }

    /// <summary>
    /// Получава политика по ID
    /// </summary>
    [HttpGet("{id}")]
    public IActionResult GetPolicy(int id)
    {
        try
        {
            Policy? policy = _policyService.GetPolicy(id);
            
            if (policy == null)
            {
                return NotFound(new { message = "Политиката не е намерена" });
            }

            return Ok(policy);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при получаване на политика {id}: {ex.Message}", ex);
            return StatusCode(500, new { message = "Грешка при получаване на политика" });
        }
    }

    /// <summary>
    /// Създава нова политика
    /// </summary>
    [HttpPost]
    public IActionResult CreatePolicy([FromBody] Policy policy)
    {
        try
        {
            if (policy == null)
            {
                return BadRequest(new { message = "Политиката е задължителна" });
            }

            Policy createdPolicy = _policyService.CreatePolicy(policy);
            _logger.LogInfo($"Създадена политика: {createdPolicy.Name} (ID: {createdPolicy.Id})");
            
            return CreatedAtAction(nameof(GetPolicy), new { id = createdPolicy.Id }, createdPolicy);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при създаване на политика: {ex.Message}", ex);
            return StatusCode(500, new { message = "Грешка при създаване на политика" });
        }
    }

    /// <summary>
    /// Обновява политика
    /// </summary>
    [HttpPut("{id}")]
    public IActionResult UpdatePolicy(int id, [FromBody] Policy policy)
    {
        try
        {
            if (policy == null)
            {
                return BadRequest(new { message = "Политиката е задължителна" });
            }

            Policy? updatedPolicy = _policyService.UpdatePolicy(id, policy);
            
            if (updatedPolicy == null)
            {
                return NotFound(new { message = "Политиката не е намерена" });
            }

            _logger.LogInfo($"Обновена политика: {updatedPolicy.Name} (ID: {id})");
            return Ok(updatedPolicy);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при обновяване на политика {id}: {ex.Message}", ex);
            return StatusCode(500, new { message = "Грешка при обновяване на политика" });
        }
    }

    /// <summary>
    /// Изтрива политика
    /// </summary>
    [HttpDelete("{id}")]
    public IActionResult DeletePolicy(int id)
    {
        try
        {
            bool deleted = _policyService.DeletePolicy(id);
            
            if (!deleted)
            {
                return NotFound(new { message = "Политиката не е намерена" });
            }

            _logger.LogInfo($"Изтрита политика (ID: {id})");
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при изтриване на политика {id}: {ex.Message}", ex);
            return StatusCode(500, new { message = "Грешка при изтриване на политика", error = ex.Message });
        }
    }

    /// <summary>
    /// Получава активни политики за конкретна машина и потребител.
    /// AllowAnonymous – Monitor услугата вика този endpoint без логин; без него политиките не се прилагат на клиентите.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("machine/{machineName}/user/{username}")]
    public IActionResult GetPoliciesForMachine(string machineName, string username)
    {
        try
        {
            List<Policy> policies = _policyService.GetActivePoliciesForMachine(machineName, username);
            return Ok(policies);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при получаване на политики за {machineName}/{username}: {ex.Message}", ex);
            return StatusCode(500, new { message = "Грешка при получаване на политики" });
        }
    }

    /// <summary>
    /// Проверява дали уебсайт е блокиран
    /// </summary>
    [HttpPost("check/website")]
    public IActionResult CheckWebsite([FromBody] WebsiteCheckRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrEmpty(request.Url))
            {
                return BadRequest(new { message = "URL е задължителен" });
            }

            bool isBlocked = _policyService.IsWebsiteBlocked(request.MachineName, request.Username, request.Url);
            return Ok(new { isBlocked, url = request.Url });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при проверка на уебсайт: {ex.Message}", ex);
            return StatusCode(500, new { message = "Грешка при проверка на уебсайт" });
        }
    }

    /// <summary>
    /// Проверява дали приложение е блокирано
    /// </summary>
    [HttpPost("check/application")]
    public IActionResult CheckApplication([FromBody] ApplicationCheckRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrEmpty(request.ApplicationName))
            {
                return BadRequest(new { message = "ApplicationName е задължителен" });
            }

            bool isBlocked = _policyService.IsApplicationBlocked(request.MachineName, request.Username, request.ApplicationName);
            return Ok(new { isBlocked, applicationName = request.ApplicationName });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при проверка на приложение: {ex.Message}", ex);
            return StatusCode(500, new { message = "Грешка при проверка на приложение" });
        }
    }

    /// <summary>
    /// Проверява дали файлово разширение е блокирано
    /// </summary>
    [HttpPost("check/fileextension")]
    public IActionResult CheckFileExtension([FromBody] FileExtensionCheckRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrEmpty(request.FileExtension))
            {
                return BadRequest(new { message = "FileExtension е задължителен" });
            }

            bool isBlocked = _policyService.IsFileExtensionBlocked(request.MachineName, request.Username, request.FileExtension);
            return Ok(new { isBlocked, fileExtension = request.FileExtension });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при проверка на файлово разширение: {ex.Message}", ex);
            return StatusCode(500, new { message = "Грешка при проверка на файлово разширение" });
        }
    }
}

public class WebsiteCheckRequest
{
    public string MachineName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class ApplicationCheckRequest
{
    public string MachineName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
}

public class FileExtensionCheckRequest
{
    public string MachineName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
}

