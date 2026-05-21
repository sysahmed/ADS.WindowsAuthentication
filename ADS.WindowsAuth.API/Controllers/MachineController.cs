using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ADS.WindowsAuth.API.Services;

namespace ADS.WindowsAuth.API.Controllers;

/// <summary>
/// API за реално-времеви данни от Machine Monitor (процеси, инсталирани програми, команди).
/// Monitor-ът вика без логин (AllowAnonymous).
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/machines")]
public class MachineController : ControllerBase
{
    private readonly MachineDataService _machineData;

    public MachineController(MachineDataService machineData)
    {
        _machineData = machineData;
    }

    /// <summary>
    /// Monitor → API: изпраща текущ snapshot (процеси + инсталирани)
    /// POST /api/machines/snapshot
    /// </summary>
    [HttpPost("snapshot")]
    public IActionResult PostSnapshot([FromBody] MachineSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot?.MachineName))
            return BadRequest("MachineName is required");

        _machineData.UpdateSnapshot(snapshot);
        return Ok(new { updated = snapshot.MachineName });
    }

    /// <summary>
    /// Web → API: взима snapshot за дадена машина
    /// GET /api/machines/{machineName}/snapshot
    /// </summary>
    [HttpGet("{machineName}/snapshot")]
    [Authorize]
    public IActionResult GetSnapshot(string machineName)
    {
        var snapshot = _machineData.GetSnapshot(machineName);
        if (snapshot == null)
            return NotFound(new { error = "Няма данни за тази машина. Monitor трябва да е стартиран." });

        return Ok(snapshot);
    }

    /// <summary>
    /// Web → API: изпраща команда към машина (kill process / uninstall)
    /// POST /api/machines/{machineName}/command
    /// </summary>
    [HttpPost("{machineName}/command")]
    [Authorize]
    public IActionResult SendCommand(string machineName, [FromBody] CommandRequest req)
    {
        if (string.IsNullOrWhiteSpace(req?.Type) || string.IsNullOrWhiteSpace(req.Argument))
            return BadRequest("Type and Argument are required");

        var cmd = _machineData.EnqueueCommand(machineName, req.Type, req.Argument);
        return Ok(new { commandId = cmd.CommandId, message = "Командата е поставена на опашката" });
    }

    /// <summary>
    /// Monitor → API: взима чакащи команди за изпълнение
    /// GET /api/machines/commands/pending?machineName=...
    /// </summary>
    [HttpGet("commands/pending")]
    public IActionResult GetPendingCommands([FromQuery] string machineName)
    {
        var cmds = _machineData.GetPendingCommands(machineName);
        return Ok(cmds);
    }

    /// <summary>
    /// Monitor → API: отчита резултат от изпълнена команда
    /// POST /api/machines/commands/{commandId}/done
    /// </summary>
    [HttpPost("commands/{commandId}/done")]
    public IActionResult MarkCommandDone(string commandId, [FromBody] CommandResultRequest req)
    {
        _machineData.MarkCommandExecuted(commandId, req?.Result ?? "OK");
        return Ok();
    }
}

public class CommandRequest
{
    public string Type { get; set; } = "";      // "kill" | "uninstall"
    public string Argument { get; set; } = "";  // PID (string) или app name
}

public class CommandResultRequest
{
    public string? Result { get; set; }
}
