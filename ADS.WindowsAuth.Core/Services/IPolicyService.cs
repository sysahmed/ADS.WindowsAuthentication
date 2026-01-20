using ADS.WindowsAuth.Core.Models;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Интерфейс за управление на политики
/// </summary>
public interface IPolicyService
{
    /// <summary>
    /// Създава нова политика
    /// </summary>
    Policy CreatePolicy(Policy policy);

    /// <summary>
    /// Обновява политика
    /// </summary>
    Policy? UpdatePolicy(int policyId, Policy policy);

    /// <summary>
    /// Изтрива политика
    /// </summary>
    bool DeletePolicy(int policyId);

    /// <summary>
    /// Получава политика по ID
    /// </summary>
    Policy? GetPolicy(int policyId);

    /// <summary>
    /// Получава всички политики
    /// </summary>
    List<Policy> GetAllPolicies();

    /// <summary>
    /// Получава активни политики за машина
    /// </summary>
    List<Policy> GetActivePoliciesForMachine(string machineName, string username);

    /// <summary>
    /// Проверява дали уебсайт е блокиран
    /// </summary>
    bool IsWebsiteBlocked(string machineName, string username, string url);

    /// <summary>
    /// Проверява дали приложение е блокирано
    /// </summary>
    bool IsApplicationBlocked(string machineName, string username, string applicationName);

    /// <summary>
    /// Проверява дали файлово разширение е блокирано
    /// </summary>
    bool IsFileExtensionBlocked(string machineName, string username, string fileExtension);
}

