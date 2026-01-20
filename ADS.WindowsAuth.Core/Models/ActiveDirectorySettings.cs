namespace ADS.WindowsAuth.Core.Models;

/// <summary>
/// Настройки за Active Directory
/// </summary>
public class ActiveDirectorySettings
{
    /// <summary>
    /// Дали AD е активиран
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Име на домейна
    /// </summary>
    public string DomainName { get; set; } = string.Empty;

    /// <summary>
    /// LDAP път (ако е празен, се генерира автоматично)
    /// </summary>
    public string LdapPath { get; set; } = string.Empty;

    /// <summary>
    /// Service Account за свързване към AD
    /// </summary>
    public string ServiceAccount { get; set; } = string.Empty;

    /// <summary>
    /// Парола на Service Account
    /// </summary>
    public string ServicePassword { get; set; } = string.Empty;

    /// <summary>
    /// Генерира LDAP път ако не е зададен
    /// </summary>
    public string GetLdapPath()
    {
        if (!string.IsNullOrEmpty(LdapPath))
        {
            return LdapPath;
        }

        // Генериране на LDAP път от домейна
        string[] domainParts = DomainName.Split('.');
        string ldapPath = "LDAP://";
        
        for (int i = 0; i < domainParts.Length; i++)
        {
            if (i > 0) ldapPath += ",";
            ldapPath += "DC=" + domainParts[i];
        }

        return ldapPath;
    }
}

