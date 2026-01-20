using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Core.Data;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Add secure configuration sources
builder.Configuration.AddSecureConfiguration();

// Configure services
ConfigureServices(builder);

var app = builder.Build();

// Configure middleware
ConfigureMiddleware(app);

// Configure endpoints
ConfigureEndpoints(app);

// Зареждане на активни сесии от базата данни при стартиране
await LoadSessionsOnStartup(app);

// Run the application
app.Run();

// Метод за зареждане на сесии при стартиране
static async Task LoadSessionsOnStartup(WebApplication app)
{
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();
            var databaseService = scope.ServiceProvider.GetService<IDatabaseService>();
            
            if (databaseService != null)
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILoggerService>();
                logger.LogInfo("Започва зареждане на активни сесии от базата данни при стартиране...");
                
                await sessionService.LoadSessionsFromDatabaseAsync(databaseService);
                
                logger.LogInfo("Зареждане на активни сесии от базата данни завършено.");
            }
            else
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILoggerService>();
                logger.LogWarning("DatabaseService не е наличен - пропускаме зареждане на сесии от базата данни");
            }
        }
    }
    catch (Exception ex)
    {
        // Логваме грешката но не спираме приложението
        try
        {
            var logger = app.Services.GetRequiredService<ILoggerService>();
            logger.LogError($"Грешка при зареждане на сесии от базата данни при стартиране: {ex.Message}", ex);
        }
        catch
        {
            // Ако не можем да логнем, игнорираме
        }
    }
}

void ConfigureServices(WebApplicationBuilder builder)
{
    // Configure routing - изключваме lowercase URLs за да запазим точното casing на endpoints
    builder.Services.AddRouting(options =>
    {
        options.LowercaseUrls = false;
        options.LowercaseQueryStrings = false;
    });

    // HttpClient Factory
    builder.Services.AddHttpClient();
    
    // Configure services - AddControllersWithViews за да поддържаме Razor Views
    builder.Services.AddControllersWithViews();
    builder.Services.AddEndpointsApiExplorer();
    
    // HttpClient за комуникация с Remote Desktop Service
    builder.Services.AddHttpClient();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
        { 
            Title = "ADS Windows Authentication API", 
            Version = "v1",
            Description = "API за Windows аутентикация с QR код"
        });
        
        // Игнорираме MVC контролерите (HomeController и т.н.) - само API контролерите
        c.DocInclusionPredicate((docName, apiDesc) =>
        {
            // Включваме само контролери с [ApiController] attribute
            return apiDesc.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor actionDescriptor &&
                   actionDescriptor.ControllerTypeInfo.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.ApiControllerAttribute), false).Any();
        });
        
        // Добавяне на JWT схема за Swagger (опционално)
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });
        
        // Не изискваме JWT за всички endpoints - само за тези които имат [Authorize]
        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // Configure database
    ConfigureDatabase(builder);

    // Configure authentication
    ConfigureAuthentication(builder);

    // Configure services
    // Определяне на application directory
    string applicationDirectory = AppDomain.CurrentDomain.BaseDirectory;
    
    // Logger Service - трябва applicationDirectory
    builder.Services.AddSingleton<ILoggerService>(sp => new EnhancedLoggerService(applicationDirectory));
    
    // Windows Auth Service
    builder.Services.AddSingleton<IWindowsAuthService, WindowsAuthService>();
    
    // Session Service - изисква IWindowsAuthService и ILoggerService
    builder.Services.AddSingleton<ISessionService>(sp => 
        new SessionService(
            sp.GetRequiredService<ILoggerService>(), 
            sp.GetRequiredService<IWindowsAuthService>()));
    
    // Activity Monitor Service - изисква ILoggerService
    builder.Services.AddSingleton<IActivityMonitorService>(sp => 
        new ActivityMonitorService(sp.GetRequiredService<ILoggerService>()));
    
    // Policy Service - изисква ILoggerService
    builder.Services.AddSingleton<IPolicyService>(sp => 
        new PolicyService(sp.GetRequiredService<ILoggerService>()));
    
    // JWT Service - трябва JwtSettings
    builder.Services.AddSingleton<IJwtService>(sp =>
    {
        // Предаваме environment информация за правилна проверка на Development режим
        bool isDevelopment = builder.Environment.IsDevelopment();
        var secureSettings = SecureConfigurationProvider.GetSecureSettings(builder.Configuration, isDevelopment);
        var jwtSettings = new JwtSettings
        {
            Issuer = builder.Configuration["Jwt:Issuer"] ?? "ADS.WindowsAuth.API",
            Audience = builder.Configuration["Jwt:Audience"] ?? "ADS.WindowsAuth.Client",
            Key = secureSettings.JwtKey,
            ExpiryMinutes = builder.Configuration.GetValue<int>("Jwt:ExpiryMinutes", 60)
        };
        
        if (!jwtSettings.IsValid())
        {
            throw new InvalidOperationException("JWT configuration is invalid");
        }
        
        return new JwtService(jwtSettings, sp.GetRequiredService<ILoggerService>());
    });
    // Active Directory Service
    builder.Services.AddSingleton<IAdService>(sp =>
    {
        var adSettings = new ADS.WindowsAuth.Core.Models.ActiveDirectorySettings
        {
            Enabled = builder.Configuration.GetValue<bool>("ActiveDirectory:Enabled", false),
            DomainName = builder.Configuration["ActiveDirectory:DomainName"] ?? "",
            LdapPath = builder.Configuration["ActiveDirectory:LdapPath"] ?? "",
            ServiceAccount = builder.Configuration["ActiveDirectory:ServiceAccount"] ?? "",
            ServicePassword = builder.Configuration["ActiveDirectory:ServicePassword"] ?? ""
        };
        return new AdService(adSettings, sp.GetRequiredService<ILoggerService>());
    });

    // Database Service (Scoped) - правилно свързване с DbContext lifecycle
    builder.Services.AddScoped<IDatabaseService>(sp =>
    {
        try
        {
            // Опитваме се да вземем ApplicationDbContext от текущия scope
            var context = sp.GetRequiredService<ApplicationDbContext>();
            var loggerService = sp.GetRequiredService<ILoggerService>();
            var adService = sp.GetService<IAdService>();
            
            // Проверка дали context-ът е валиден
            if (context == null)
            {
                loggerService.LogWarning("ApplicationDbContext не е наличен - използвам MockDatabaseService");
                return new MockDatabaseService(loggerService);
            }
            
            return new DatabaseService(context, loggerService, adService);
        }
        catch (InvalidOperationException)
        {
            // DbContext не е наличен в scope-а - използваме Mock
            var logger = sp.GetService<ILoggerService>();
            logger?.LogWarning("ApplicationDbContext не е наличен в scope - използвам MockDatabaseService");
            return new MockDatabaseService(logger!);
        }
        catch (Exception ex)
        {
            var logger = sp.GetService<ILoggerService>();
            logger?.LogError($"Грешка при създаване на DatabaseService: {ex.Message}. Използвам MockDatabaseService.", ex);
            return new MockDatabaseService(logger!);
        }
    });

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });
}

void ConfigureDatabase(WebApplicationBuilder builder)
{
    string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        // Използваме InMemory database като fallback
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("ADS_WindowsAuth_Memory_Fallback"), 
            ServiceLifetime.Scoped);
    }
    else
    {
        // SQL Server с правилна конфигурация
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString,
                sqlServerOptions => sqlServerOptions
                    .EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null)
                    .CommandTimeout(60)),
            ServiceLifetime.Scoped); // Явно задаваме Scoped lifetime
    }
}

void ConfigureAuthentication(WebApplicationBuilder builder)
{
    // JWT configuration from appsettings
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ADS.WindowsAuth.API";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ADS.WindowsAuth.Client";
    
    // Предаваме environment информация за правилна проверка на Development режим
    bool isDevelopment = builder.Environment.IsDevelopment();
    var jwtKey = SecureConfigurationProvider.GetSecureSettings(builder.Configuration, isDevelopment).JwtKey;
    
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.Zero // Не позволяваме clock skew за по-строга валидация
            };
            
            // Подобрена обработка на грешки при валидация на токен
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetService<ILoggerService>();
                    logger?.LogError($"JWT Authentication failed: {context.Exception.Message}", context.Exception);
                    
                    // Не хвърляме exception - позволяваме на приложението да продължи
                    context.NoResult();
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetService<ILoggerService>();
                    logger?.LogInfo($"JWT Token validated successfully for user: {context.Principal?.Identity?.Name}");
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetService<ILoggerService>();
                    logger?.LogWarning($"JWT Challenge triggered: {context.Error} - {context.ErrorDescription}");
                    return Task.CompletedTask;
                }
            };
        });
}

void ConfigureMiddleware(WebApplication app)
{
    // Логване на всички заявки за debugging - ПРЕДИ всичко друго
    app.Use(async (context, next) =>
    {
        var logger = context.RequestServices.GetService<ILoggerService>();
        logger?.LogInfo($"[MIDDLEWARE] Заявка: {context.Request.Method} {context.Request.Path}{context.Request.QueryString}");
        await next();
    });
    
    // Exception handling - трябва да е първо
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler("/Home/Error");
    }

    // Global exception handling middleware - преди UseRouting
    app.Use(async (context, next) =>
    {
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetService<ILoggerService>();
            logger?.LogError($"Грешка: {context.Request.Method} {context.Request.Path} - {ex.Message}", ex);
            
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
            }
        }
    });

    app.UseRouting();
    
    // Static files за CSS, JS, images и т.н.
    app.UseStaticFiles();
    
    app.UseCors("AllowAll");
    
    app.UseAuthentication();
    app.UseAuthorization();
    
    // Swagger middleware - след Authentication но преди endpoints
    // Работи и в Development и в Production
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ADS Windows Auth API v1");
        c.RoutePrefix = "swagger"; // Swagger ще е достъпен на /swagger
        c.DisplayRequestDuration();
    });
}

void ConfigureEndpoints(WebApplication app)
{
    // Health checks endpoint
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.Now }));

    // Map default controller route за Views (Home/Index и т.н.) - ПРЕДИ MapControllers
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
    
    // Map controllers - с поддръжка на Views
    app.MapControllers();
}
