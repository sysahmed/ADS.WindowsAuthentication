using System.Collections.Concurrent;
using System.Text.Json;
using ADS.WindowsAuth.Core.Models;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Записва чакащи събития във файл в OfflineStoragePath. При прекъсване на връзката
/// събитията се събират локално и се прехвърлят при възстановяване.
/// </summary>
public class OfflineEventBuffer : IOfflineEventBuffer
{
    private readonly string _storagePath;
    private readonly ILoggerService _logger;
    private readonly ConcurrentDictionary<string, OfflineEvent> _memory = new();
    private readonly object _fileLock = new();
    private readonly int _maxRetentionDays;
    private const string FileName = "pending_events.json";

    public OfflineEventBuffer(ServiceConfiguration config, ILoggerService logger)
    {
        var path = string.IsNullOrWhiteSpace(config.OfflineStoragePath)
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OfflineData")
            : config.OfflineStoragePath;
        _storagePath = Path.Combine(path, FileName);
        _logger = logger;
        _maxRetentionDays = config.OfflineDataRetention > 0 ? config.OfflineDataRetention : 7;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_storagePath)!);
            LoadFromDisk();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"OfflineEventBuffer: не може да се създаде директория: {ex.Message}");
        }
    }

    public void Enqueue(string endpoint, string payloadJson)
    {
        var evt = new OfflineEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Endpoint = endpoint,
            PayloadJson = payloadJson,
            CreatedAt = DateTime.Now
        };
        _memory.TryAdd(evt.Id, evt);
        SaveToDisk();
        _logger.LogInfo($"OfflineEventBuffer: добавено събитие {evt.Endpoint} (всичко: {_memory.Count})");
    }

    public IReadOnlyList<OfflineEvent> GetPending()
    {
        PurgeOld();
        return _memory.Values.OrderBy(e => e.CreatedAt).ToList();
    }

    public void Remove(string eventId)
    {
        if (_memory.TryRemove(eventId, out _))
            SaveToDisk();
    }

    public int PendingCount => _memory.Count;

    private void LoadFromDisk()
    {
        if (!File.Exists(_storagePath)) return;

        lock (_fileLock)
        {
            try
            {
                var json = File.ReadAllText(_storagePath);
                var list = JsonSerializer.Deserialize<List<OfflineEvent>>(json);
                if (list != null)
                {
                    foreach (var e in list)
                    {
                        if (!string.IsNullOrEmpty(e.Id) && e.CreatedAt.AddDays(_maxRetentionDays) > DateTime.Now)
                            _memory.TryAdd(e.Id, e);
                    }
                    _logger.LogInfo($"OfflineEventBuffer: заредени {_memory.Count} чакащи събития от диск");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"OfflineEventBuffer: грешка при четене: {ex.Message}");
            }
        }
    }

    private void SaveToDisk()
    {
        lock (_fileLock)
        {
            try
            {
                var list = _memory.Values.ToList();
                var json = JsonSerializer.Serialize(list);
                File.WriteAllText(_storagePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"OfflineEventBuffer: грешка при запис: {ex.Message}");
            }
        }
    }

    private void PurgeOld()
    {
        var cutoff = DateTime.Now.AddDays(-_maxRetentionDays);
        var toRemove = _memory.Where(kv => kv.Value.CreatedAt < cutoff).Select(kv => kv.Key).ToList();
        foreach (var id in toRemove)
            _memory.TryRemove(id, out _);
        if (toRemove.Count > 0)
            SaveToDisk();
    }
}
