using System.Collections.Concurrent;

namespace ADS.WindowsAuth.API.Services;

public class ProcessInfo
{
    public int Pid { get; set; }
    public string Name { get; set; } = "";
    public string? MainWindowTitle { get; set; }
    public long MemoryMb { get; set; }
    public string? Username { get; set; }
    public DateTime StartTime { get; set; }
}

public class InstalledApp
{
    public string Name { get; set; } = "";
    public string? Version { get; set; }
    public string? Publisher { get; set; }
    public string? InstallDate { get; set; }
}

public class MachineSnapshot
{
    public string MachineName { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
    public List<ProcessInfo> Processes { get; set; } = new();
    public List<InstalledApp> InstalledApps { get; set; } = new();
}

public class MachineCommand
{
    public string CommandId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string MachineName { get; set; } = "";
    public string Type { get; set; } = ""; // "kill" | "uninstall"
    public string Argument { get; set; } = ""; // PID or app name
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool Executed { get; set; }
    public string? Result { get; set; }
}

/// <summary>
/// In-memory store for real-time machine process/app data and commands.
/// </summary>
public class MachineDataService
{
    private readonly ConcurrentDictionary<string, MachineSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, MachineCommand> _commands = new();

    // ── Snapshots ──────────────────────────────────────────────────────────

    public void UpdateSnapshot(MachineSnapshot snapshot)
    {
        snapshot.UpdatedAt = DateTime.UtcNow;
        _snapshots[snapshot.MachineName] = snapshot;
    }

    public MachineSnapshot? GetSnapshot(string machineName) =>
        _snapshots.TryGetValue(machineName, out var s) ? s : null;

    public IReadOnlyList<string> GetKnownMachines() =>
        _snapshots.Keys.ToList();

    // ── Commands ───────────────────────────────────────────────────────────

    public MachineCommand EnqueueCommand(string machineName, string type, string argument)
    {
        var cmd = new MachineCommand { MachineName = machineName, Type = type, Argument = argument };
        _commands[cmd.CommandId] = cmd;
        return cmd;
    }

    public List<MachineCommand> GetPendingCommands(string machineName) =>
        _commands.Values
            .Where(c => string.Equals(c.MachineName, machineName, StringComparison.OrdinalIgnoreCase) && !c.Executed)
            .ToList();

    public void MarkCommandExecuted(string commandId, string result)
    {
        if (_commands.TryGetValue(commandId, out var cmd))
        {
            cmd.Executed = true;
            cmd.Result = result;
        }
    }
}
