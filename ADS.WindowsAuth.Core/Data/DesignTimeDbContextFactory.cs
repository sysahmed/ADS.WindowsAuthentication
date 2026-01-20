using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ADS.WindowsAuth.Core.Data;

/// <summary>
/// Factory за създаване на DbContext при design time (миграции)
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // При design time използваме connection string от appsettings.json или environment variable
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        
        // Първо опитай да вземеш connection string от environment variable
        string? connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        
        // Ако няма environment variable, използвай default connection string за миграции
        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = "Server=10.168.0.252;Database=ADS_WindowsAuth;User Id=sa;Password=wrjkd34mk22;TrustServerCertificate=true;";
        }
        
        optionsBuilder.UseSqlServer(
            connectionString,
            sqlServerOptions => sqlServerOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null));

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}

