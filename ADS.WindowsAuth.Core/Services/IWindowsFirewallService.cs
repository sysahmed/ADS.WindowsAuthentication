namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Интерфейс за управление на Windows Firewall правила
/// </summary>
public interface IWindowsFirewallService
{
    /// <summary>
    /// Блокира домейн чрез Windows Firewall
    /// </summary>
    Task<bool> BlockDomainAsync(string domain);

    /// <summary>
    /// Разблокира домейн
    /// </summary>
    Task<bool> UnblockDomainAsync(string domain);

    /// <summary>
    /// Блокира IP адрес
    /// </summary>
    Task<bool> BlockIpAddressAsync(string ipAddress);

    /// <summary>
    /// Разблокира IP адрес
    /// </summary>
    Task<bool> UnblockIpAddressAsync(string ipAddress);

    /// <summary>
    /// Блокира порт
    /// </summary>
    Task<bool> BlockPortAsync(int port, string protocol = "TCP");

    /// <summary>
    /// Разблокира порт
    /// </summary>
    Task<bool> UnblockPortAsync(int port, string protocol = "TCP");

    /// <summary>
    /// Получава всички блокирани домейни
    /// </summary>
    Task<List<string>> GetBlockedDomainsAsync();

    /// <summary>
    /// Проверява дали домейн е блокиран
    /// </summary>
    Task<bool> IsDomainBlockedAsync(string domain);
}

