using ADS.WindowsAuth.Core.Models;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Интерфейс за мониторинг на активност
/// </summary>
public interface IActivityMonitorService
{
    /// <summary>
    /// Започва мониторинг на активност за потребител
    /// </summary>
    void StartMonitoring(string username, string domain, string machineName);

    /// <summary>
    /// Спира мониторинг на активност
    /// </summary>
    void StopMonitoring(string username, string machineName);

    /// <summary>
    /// Регистрира отваряне на файл
    /// </summary>
    void RegisterFileOpen(string username, string machineName, string filePath, string applicationName);

    /// <summary>
    /// Регистрира затваряне на файл
    /// </summary>
    void RegisterFileClose(string username, string machineName, string filePath);

    /// <summary>
    /// Регистрира посещение на уебсайт
    /// </summary>
    void RegisterWebsiteVisit(string username, string machineName, string url, string title, string browser, int durationSeconds);

    /// <summary>
    /// Регистрира стартиране на приложение
    /// </summary>
    void RegisterApplicationStart(string username, string machineName, string applicationName, string executablePath);

    /// <summary>
    /// Регистрира затваряне на приложение
    /// </summary>
    void RegisterApplicationClose(string username, string machineName, string applicationName);

    /// <summary>
    /// Обновява времето на екрана
    /// </summary>
    void UpdateScreenTime(string username, string machineName, int seconds);

    /// <summary>
    /// Получава активността за потребител
    /// </summary>
    UserActivity? GetUserActivity(string username, string machineName);

    /// <summary>
    /// Получава всички активности
    /// </summary>
    List<UserActivity> GetAllActivities(DateTime? fromDate = null, DateTime? toDate = null);

    /// <summary>
    /// Премахва всички активности за дадена машина
    /// </summary>
    bool RemoveMachine(string machineName);
}

