using Microsoft.AspNetCore.Mvc;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;

namespace ADS.WindowsAuth.Service.Controllers;

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

    [HttpGet]
    public IActionResult GetAllPolicies()
    {
        List<Policy> policies = _policyService.GetAllPolicies();
        return Ok(policies);
    }

    [HttpGet("{id}")]
    public IActionResult GetPolicy(int id)
    {
        Policy? policy = _policyService.GetPolicy(id);
        
        if (policy == null)
        {
            return NotFound(new { message = "Политиката не е намерена" });
        }

        return Ok(policy);
    }

    [HttpPost]
    public IActionResult CreatePolicy([FromBody] Policy policy)
    {
        Policy createdPolicy = _policyService.CreatePolicy(policy);
        return CreatedAtAction(nameof(GetPolicy), new { id = createdPolicy.Id }, createdPolicy);
    }

    [HttpPut("{id}")]
    public IActionResult UpdatePolicy(int id, [FromBody] Policy policy)
    {
        Policy? updatedPolicy = _policyService.UpdatePolicy(id, policy);
        
        if (updatedPolicy == null)
        {
            return NotFound(new { message = "Политиката не е намерена" });
        }

        return Ok(updatedPolicy);
    }

    [HttpDelete("{id}")]
    public IActionResult DeletePolicy(int id)
    {
        bool deleted = _policyService.DeletePolicy(id);
        
        if (!deleted)
        {
            return NotFound(new { message = "Политиката не е намерена" });
        }

        return NoContent();
    }

    [HttpGet("machine/{machineName}/user/{username}")]
    public IActionResult GetPoliciesForMachine(string machineName, string username)
    {
        List<Policy> policies = _policyService.GetActivePoliciesForMachine(machineName, username);
        return Ok(policies);
    }

    [HttpPost("check/website")]
    public IActionResult CheckWebsite([FromBody] WebsiteCheckRequest request)
    {
        bool isBlocked = _policyService.IsWebsiteBlocked(request.MachineName, request.Username, request.Url);
        return Ok(new { isBlocked });
    }

    [HttpPost("check/application")]
    public IActionResult CheckApplication([FromBody] ApplicationCheckRequest request)
    {
        bool isBlocked = _policyService.IsApplicationBlocked(request.MachineName, request.Username, request.ApplicationName);
        return Ok(new { isBlocked });
    }

    [HttpPost("check/fileextension")]
    public IActionResult CheckFileExtension([FromBody] FileExtensionCheckRequest request)
    {
        bool isBlocked = _policyService.IsFileExtensionBlocked(request.MachineName, request.Username, request.FileExtension);
        return Ok(new { isBlocked });
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

