using System.Collections.Concurrent;
using System.Text.Json;
using ADS.WindowsAuth.Core.Data.Entities;
using ADS.WindowsAuth.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Реализация на сервис за управление на политики
/// </summary>
public class PolicyService : IPolicyService
{
    private readonly ConcurrentDictionary<int, Policy> _policies = new();
    private int _nextId = 1;
    private readonly ILoggerService _logger;
    private readonly IServiceScopeFactory? _scopeFactory;

    /// <summary>
    /// Конструктор за API с достъп до БД (scopeFactory подаден).
    /// </summary>
    public PolicyService(ILoggerService logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Конструктор за Monitor/Service без БД – политиките остават само в паметта.
    /// </summary>
    public PolicyService(ILoggerService logger)
    {
        _logger = logger;
        _scopeFactory = null;
    }

    public Policy CreatePolicy(Policy policy)
    {
        policy.CreatedAt = DateTime.Now;
        policy.UpdatedAt = DateTime.Now;

        if (_scopeFactory != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
            var entity = ToEntity(policy);
            var id = db.SavePolicyAsync(entity).GetAwaiter().GetResult();
            policy.Id = id;
        }
        else
        {
            policy.Id = Interlocked.Increment(ref _nextId);
        }

        _policies.TryAdd(policy.Id, policy);
        _logger.LogInfo($"Създадена политика: {policy.Name} (ID: {policy.Id})");

        return policy;
    }

    public Policy? UpdatePolicy(int policyId, Policy policy)
    {
        policy.Id = policyId;
        EnsureLoadedFromDb();
        if (_policies.TryGetValue(policyId, out Policy? existingPolicy))
        {
            policy.CreatedAt = existingPolicy.CreatedAt;
        }
        policy.UpdatedAt = DateTime.Now;

        if (_scopeFactory != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
            var entity = ToEntity(policy);
            db.UpdatePolicyAsync(entity).GetAwaiter().GetResult();
        }

        _policies.AddOrUpdate(policyId, policy, (_, _) => policy);
        _logger.LogInfo($"Обновена политика: {policy.Name} (ID: {policyId})");

        return policy;
    }

    public bool DeletePolicy(int policyId)
    {
        if (_scopeFactory != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
            db.DeletePolicyAsync(policyId).GetAwaiter().GetResult();
        }

        if (_policies.TryRemove(policyId, out _))
        {
            _logger.LogInfo($"Изтрита политика (ID: {policyId})");
            return true;
        }

        return true;
    }

    public Policy? GetPolicy(int policyId)
    {
        EnsureLoadedFromDb();
        _policies.TryGetValue(policyId, out Policy? policy);
        return policy;
    }

    public List<Policy> GetAllPolicies()
    {
        EnsureLoadedFromDb();
        return _policies.Values.ToList();
    }

    public List<Policy> GetActivePoliciesForMachine(string machineName, string username)
    {
        EnsureLoadedFromDb();
        return _policies.Values
            .Where(p => p.IsActive)
            .Where(p => p.TargetMachines.Count == 0 || p.TargetMachines.Any(m => string.Equals(m, machineName, StringComparison.OrdinalIgnoreCase)))
            .Where(p => p.TargetUsers.Count == 0 || p.TargetUsers.Any(u => string.Equals(u, username, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public bool IsWebsiteBlocked(string machineName, string username, string url)
    {
        var policies = GetActivePoliciesForMachine(machineName, username);

        foreach (var policy in policies)
        {
            foreach (var blockedSite in policy.BlockedWebsites)
            {
                if (url.Contains(blockedSite, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool IsApplicationBlocked(string machineName, string username, string applicationName)
    {
        var policies = GetActivePoliciesForMachine(machineName, username);

        foreach (var policy in policies)
        {
            // Whitelist режим: блокирай всичко, което НЕ е в AllowedApplications
            if (policy.AppWhitelistMode && policy.AllowedApplications.Count > 0)
            {
                bool isAllowed = policy.AllowedApplications.Any(a =>
                    applicationName.Contains(a, StringComparison.OrdinalIgnoreCase) ||
                    a.Contains(applicationName, StringComparison.OrdinalIgnoreCase));
                if (!isAllowed) return true;
            }

            // Blacklist режим: блокирай само изрично изброените
            foreach (var blockedApp in policy.BlockedApplications)
            {
                if (applicationName.Contains(blockedApp, StringComparison.OrdinalIgnoreCase) ||
                    blockedApp.Contains(applicationName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool IsWebsiteAllowed(string machineName, string username, string url)
    {
        var policies = GetActivePoliciesForMachine(machineName, username);
        foreach (var policy in policies)
        {
            if (policy.WebWhitelistMode && policy.AllowedWebsites.Count > 0)
            {
                bool isAllowed = policy.AllowedWebsites.Any(s =>
                    url.Contains(s, StringComparison.OrdinalIgnoreCase));
                if (!isAllowed) return false;
            }
        }
        return true;
    }

    public bool IsFileExtensionBlocked(string machineName, string username, string fileExtension)
    {
        var policies = GetActivePoliciesForMachine(machineName, username);

        string ext = fileExtension.StartsWith(".") ? fileExtension : "." + fileExtension;

        foreach (var policy in policies)
        {
            if (policy.BlockedFileExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureLoadedFromDb()
    {
        if (_policies.Count > 0 || _scopeFactory == null) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
        var entities = db.GetAllPoliciesAsync().GetAwaiter().GetResult();
        foreach (var e in entities)
        {
            var p = FromEntity(e);
            _policies.TryAdd(p.Id, p);
        }
    }

    private static string ToJson(List<string> list)
    {
        return list == null || list.Count == 0 ? "[]" : JsonSerializer.Serialize(list);
    }

    private static List<string> FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]") return new List<string>();
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static PolicyEntity ToEntity(Policy p)
    {
        return new PolicyEntity
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description ?? string.Empty,
            IsActive = p.IsActive,
            BlockedWebsitesJson = ToJson(p.BlockedWebsites),
            BlockedApplicationsJson = ToJson(p.BlockedApplications),
            BlockedFileExtensionsJson = ToJson(p.BlockedFileExtensions),
            TargetMachinesJson = ToJson(p.TargetMachines),
            TargetUsersJson = ToJson(p.TargetUsers),
            MaxScreenTimeSeconds = p.MaxScreenTimeSeconds,
            AllowedInstallationsJson = ToJson(p.AllowedInstallations),
            BlockedInstallationsJson = ToJson(p.BlockedInstallations),
            AllowedApplicationsJson = ToJson(p.AllowedApplications),
            AppWhitelistMode = p.AppWhitelistMode,
            AllowedWebsitesJson = ToJson(p.AllowedWebsites),
            WebWhitelistMode = p.WebWhitelistMode,
            BlockUsbAccess = p.BlockUsbAccess,
            BlockPrinterAccess = p.BlockPrinterAccess,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }

    private static Policy FromEntity(PolicyEntity e)
    {
        return new Policy
        {
            Id = e.Id,
            Name = e.Name,
            Description = e.Description ?? string.Empty,
            IsActive = e.IsActive,
            BlockedWebsites = FromJson(e.BlockedWebsitesJson),
            BlockedApplications = FromJson(e.BlockedApplicationsJson),
            BlockedFileExtensions = FromJson(e.BlockedFileExtensionsJson),
            TargetMachines = FromJson(e.TargetMachinesJson),
            TargetUsers = FromJson(e.TargetUsersJson),
            MaxScreenTimeSeconds = e.MaxScreenTimeSeconds,
            AllowedInstallations = FromJson(e.AllowedInstallationsJson),
            BlockedInstallations = FromJson(e.BlockedInstallationsJson),
            AllowedApplications = FromJson(e.AllowedApplicationsJson),
            AppWhitelistMode = e.AppWhitelistMode,
            AllowedWebsites = FromJson(e.AllowedWebsitesJson),
            WebWhitelistMode = e.WebWhitelistMode,
            BlockUsbAccess = e.BlockUsbAccess,
            BlockPrinterAccess = e.BlockPrinterAccess,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        };
    }
}
