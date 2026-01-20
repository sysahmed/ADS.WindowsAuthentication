namespace ADS.WindowsAuth.Core.Models;

/// <summary>
/// Заявка за създаване на сесия с опционални потребител и домейн
/// </summary>
public class CreateSessionRequest
{
    /// <summary>
    /// Потребителско име (опционално - ако не е предоставено, използва се текущия потребител от сървъра)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Домейн (опционално - ако не е предоставен, използва се текущия домейн от сървъра)
    /// </summary>
    public string? Domain { get; set; }
}

