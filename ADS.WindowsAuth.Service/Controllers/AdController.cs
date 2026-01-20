using Microsoft.AspNetCore.Mvc;
using ADS.WindowsAuth.Core.Services;

namespace ADS.WindowsAuth.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdController : ControllerBase
{
    private readonly IAdService _adService;
    private readonly ILoggerService _logger;

    public AdController(IAdService adService, ILoggerService logger)
    {
        _adService = adService;
        _logger = logger;
    }

    [HttpGet("enabled")]
    public IActionResult IsEnabled()
    {
        return Ok(new { enabled = _adService.IsEnabled });
    }

    [HttpPost("validate")]
    public IActionResult ValidateCredentials([FromBody] ValidateCredentialsRequest request)
    {
        if (!_adService.IsEnabled)
        {
            return BadRequest(new { message = "Active Directory не е активиран" });
        }

        bool isValid = _adService.ValidateCredentials(request.Username, request.Password);
        
        if (isValid)
        {
            _logger.LogInfo($"Успешна AD валидация за {request.Username}");
        }
        else
        {
            _logger.LogWarning($"Неуспешна AD валидация за {request.Username}");
        }

        return Ok(new { isValid });
    }

    [HttpGet("user/{username}/exists")]
    public IActionResult UserExists(string username)
    {
        if (!_adService.IsEnabled)
        {
            return BadRequest(new { message = "Active Directory не е активиран" });
        }

        bool exists = _adService.UserExists(username);
        return Ok(new { exists });
    }

    [HttpGet("user/{username}")]
    public IActionResult GetUserInfo(string username)
    {
        if (!_adService.IsEnabled)
        {
            return BadRequest(new { message = "Active Directory не е активиран" });
        }

        AdUserInfo? userInfo = _adService.GetUserInfo(username);
        
        if (userInfo == null)
        {
            return NotFound(new { message = "Потребителят не е намерен" });
        }

        return Ok(userInfo);
    }

    [HttpGet("users")]
    public IActionResult GetAllUsers()
    {
        if (!_adService.IsEnabled)
        {
            return BadRequest(new { message = "Active Directory не е активиран" });
        }

        List<AdUserInfo> users = _adService.GetAllUsers();
        return Ok(users);
    }

    [HttpGet("user/{username}/groups")]
    public IActionResult GetUserGroups(string username)
    {
        if (!_adService.IsEnabled)
        {
            return BadRequest(new { message = "Active Directory не е активиран" });
        }

        List<string> groups = _adService.GetUserGroups(username);
        return Ok(groups);
    }
}

public class ValidateCredentialsRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

