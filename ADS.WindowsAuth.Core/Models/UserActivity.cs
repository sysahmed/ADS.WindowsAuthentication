namespace ADS.WindowsAuth.Core.Models;

/// <summary>
/// Активност на потребител
/// </summary>
public class UserActivity
{
    /// <summary>
    /// Идентификатор на активността
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Потребителско име
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Домейн
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Име на машината
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Време на начало на активността
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Време на край на активността
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Общо време на екрана в секунди
    /// </summary>
    public int ScreenTimeSeconds { get; set; }

    /// <summary>
    /// Списък с отворени файлове
    /// </summary>
    public List<OpenedFile> OpenedFiles { get; set; } = new();

    /// <summary>
    /// Списък с посещени сайтове
    /// </summary>
    public List<VisitedWebsite> VisitedWebsites { get; set; } = new();

    /// <summary>
    /// Списък с отворени приложения
    /// </summary>
    public List<OpenedApplication> OpenedApplications { get; set; } = new();
}

/// <summary>
/// Отворен файл
/// </summary>
public class OpenedFile
{
    /// <summary>
    /// Път до файла
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Време на отваряне
    /// </summary>
    public DateTime OpenedAt { get; set; }

    /// <summary>
    /// Време на затваряне
    /// </summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// Приложение, което е отворило файла
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;
}

/// <summary>
/// Посетен уебсайт
/// </summary>
public class VisitedWebsite
{
    /// <summary>
    /// URL на сайта
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Заглавие на страницата
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Време на посещение
    /// </summary>
    public DateTime VisitedAt { get; set; }

    /// <summary>
    /// Браузър
    /// </summary>
    public string Browser { get; set; } = string.Empty;

    /// <summary>
    /// Продължителност на посещението в секунди
    /// </summary>
    public int DurationSeconds { get; set; }
}

/// <summary>
/// Отворено приложение
/// </summary>
public class OpenedApplication
{
    /// <summary>
    /// Име на приложението
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// Път до изпълнимия файл
    /// </summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// Време на стартиране
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Време на затваряне
    /// </summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// Общо време на използване в секунди
    /// </summary>
    public int UsageTimeSeconds { get; set; }
}

