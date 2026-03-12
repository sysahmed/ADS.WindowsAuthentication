using System.Collections.Concurrent;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Реализация на сервис за мониторинг на активност
/// </summary>
public class ActivityMonitorService : IActivityMonitorService
{
    private readonly ConcurrentDictionary<string, UserActivity> _activities = new();
    private readonly ILoggerService _logger;

    /// <summary>
    /// Конструктор
    /// </summary>
    public ActivityMonitorService(ILoggerService logger)
    {
        _logger = logger;
    }

    public void StartMonitoring(string username, string domain, string machineName)
    {
        string key = GetKey(username, machineName);
        
        UserActivity activity = new UserActivity
        {
            Username = username,
            Domain = domain,
            MachineName = machineName,
            StartTime = DateTime.Now,
            ScreenTimeSeconds = 0
        };

        _activities.AddOrUpdate(key, activity, (k, v) => activity);
        _logger.LogInfo($"Започнат мониторинг за {username}@{domain} на {machineName}");
    }

    public void StopMonitoring(string username, string machineName)
    {
        string key = GetKey(username, machineName);
        
        if (_activities.TryGetValue(key, out UserActivity? activity))
        {
            activity.EndTime = DateTime.Now;
            _logger.LogInfo($"Спрян мониторинг за {username} на {machineName}. Общо време: {activity.ScreenTimeSeconds} секунди");
        }
    }

    public void RegisterFileOpen(string username, string machineName, string filePath, string applicationName)
    {
        string key = GetKey(username, machineName);
        
        if (_activities.TryGetValue(key, out UserActivity? activity))
        {
            activity.OpenedFiles.Add(new OpenedFile
            {
                FilePath = filePath,
                OpenedAt = DateTime.Now,
                ApplicationName = applicationName
            });
        }
    }

    public void RegisterFileClose(string username, string machineName, string filePath)
    {
        string key = GetKey(username, machineName);
        
        if (_activities.TryGetValue(key, out UserActivity? activity))
        {
            OpenedFile? file = activity.OpenedFiles.FirstOrDefault(f => f.FilePath == filePath && f.ClosedAt == null);
            if (file != null)
            {
                file.ClosedAt = DateTime.Now;
            }
        }
    }

    public void RegisterWebsiteVisit(string username, string machineName, string url, string title, string browser, int durationSeconds)
    {
        string key = GetKey(username, machineName);
        
        if (_activities.TryGetValue(key, out UserActivity? activity))
        {
            activity.VisitedWebsites.Add(new VisitedWebsite
            {
                Url = url,
                Title = title,
                VisitedAt = DateTime.Now,
                Browser = browser,
                DurationSeconds = durationSeconds
            });
        }
    }

    public void RegisterApplicationStart(string username, string machineName, string applicationName, string executablePath)
    {
        string key = GetKey(username, machineName);
        
        if (_activities.TryGetValue(key, out UserActivity? activity))
        {
            activity.OpenedApplications.Add(new OpenedApplication
            {
                ApplicationName = applicationName,
                ExecutablePath = executablePath,
                StartedAt = DateTime.Now
            });
        }
    }

    public void RegisterApplicationClose(string username, string machineName, string applicationName)
    {
        string key = GetKey(username, machineName);
        
        if (_activities.TryGetValue(key, out UserActivity? activity))
        {
            OpenedApplication? app = activity.OpenedApplications.FirstOrDefault(a => a.ApplicationName == applicationName && a.ClosedAt == null);
            if (app != null)
            {
                app.ClosedAt = DateTime.Now;
                app.UsageTimeSeconds = (int)(app.ClosedAt.Value - app.StartedAt).TotalSeconds;
            }
        }
    }

    public void UpdateScreenTime(string username, string machineName, int seconds)
    {
        string key = GetKey(username, machineName);
        
        if (_activities.TryGetValue(key, out UserActivity? activity))
        {
            activity.ScreenTimeSeconds = seconds;
        }
    }

    public UserActivity? GetUserActivity(string username, string machineName)
    {
        string key = GetKey(username, machineName);
        _activities.TryGetValue(key, out UserActivity? activity);
        return activity;
    }

    public List<UserActivity> GetAllActivities(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var activities = _activities.Values.ToList();

        if (fromDate.HasValue)
        {
            activities = activities.Where(a => a.StartTime >= fromDate.Value).ToList();
        }

        if (toDate.HasValue)
        {
            activities = activities.Where(a => a.StartTime <= toDate.Value).ToList();
        }

        return activities;
    }

    public bool RemoveMachine(string machineName)
    {
        var keysToRemove = _activities.Keys
            .Where(k => k.EndsWith($"@{machineName}", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (keysToRemove.Count == 0)
            return false;

        foreach (var key in keysToRemove)
            _activities.TryRemove(key, out _);

        _logger.LogInfo($"Премахнати {keysToRemove.Count} запис(а) за машина {machineName}");
        return true;
    }

    private string GetKey(string username, string machineName)
    {
        return $"{username}@{machineName}";
    }
}

