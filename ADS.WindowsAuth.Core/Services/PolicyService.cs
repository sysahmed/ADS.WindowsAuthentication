using System.Collections.Concurrent;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Реализация на сервис за управление на политики
/// </summary>
public class PolicyService : IPolicyService
{
    private readonly ConcurrentDictionary<int, Policy> _policies = new();
    private int _nextId = 1;
    private readonly ILoggerService _logger;

    public PolicyService(ILoggerService logger)
    {
        _logger = logger;
    }

    public Policy CreatePolicy(Policy policy)
    {
        policy.Id = Interlocked.Increment(ref _nextId);
        policy.CreatedAt = DateTime.Now;
        policy.UpdatedAt = DateTime.Now;
        
        _policies.TryAdd(policy.Id, policy);
        _logger.LogInfo($"Създадена политика: {policy.Name} (ID: {policy.Id})");
        
        return policy;
    }

    public Policy? UpdatePolicy(int policyId, Policy policy)
    {
        if (_policies.TryGetValue(policyId, out Policy? existingPolicy))
        {
            policy.Id = policyId;
            policy.CreatedAt = existingPolicy.CreatedAt;
            policy.UpdatedAt = DateTime.Now;
            
            _policies.TryUpdate(policyId, policy, existingPolicy);
            _logger.LogInfo($"Обновена политика: {policy.Name} (ID: {policyId})");
            
            return policy;
        }
        
        return null;
    }

    public bool DeletePolicy(int policyId)
    {
        if (_policies.TryRemove(policyId, out _))
        {
            _logger.LogInfo($"Изтрита политика (ID: {policyId})");
            return true;
        }
        
        return false;
    }

    public Policy? GetPolicy(int policyId)
    {
        _policies.TryGetValue(policyId, out Policy? policy);
        return policy;
    }

    public List<Policy> GetAllPolicies()
    {
        return _policies.Values.ToList();
    }

    public List<Policy> GetActivePoliciesForMachine(string machineName, string username)
    {
        return _policies.Values
            .Where(p => p.IsActive)
            .Where(p => p.TargetMachines.Count == 0 || p.TargetMachines.Contains(machineName))
            .Where(p => p.TargetUsers.Count == 0 || p.TargetUsers.Contains(username))
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
}

