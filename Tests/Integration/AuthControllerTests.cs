using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Core.Models;
using Moq;
using Xunit;
using FluentAssertions;
using System.Net.Http;
using System.Text.Json;

namespace ADS.WindowsAuth.Tests.Integration;

/// <summary>
/// Integration tests for AuthController
/// </summary>
public class AuthControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IWindowsAuthService> _mockWindowsAuthService;
    private readonly Mock<ISessionService> _mockSessionService;

    public AuthControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing services
                var windowsAuthServiceDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IWindowsAuthService));
                if (windowsAuthServiceDescriptor != null)
                {
                    services.Remove(windowsAuthServiceDescriptor);
                }

                var sessionServiceDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(ISessionService));
                if (sessionServiceDescriptor != null)
                {
                    services.Remove(sessionServiceDescriptor);
                }

                // Add mocked services
                _mockWindowsAuthService = new Mock<IWindowsAuthService>();
                _mockSessionService = new Mock<ISessionService>();
                
                services.AddSingleton(_mockWindowsAuthService.Object);
                services.AddSingleton(_mockSessionService.Object);
            });
        });
    }

    [Fact]
    public async Task CreateSession_WithValidRequest_ShouldReturnSession()
    {
        // Arrange
        var expectedSession = new AuthSession
        {
            SessionId = "test-session-id",
            AccessToken = "test-access-token",
            MachineName = "test-machine",
            WindowsUsername = "testuser",
            Domain = "testdomain",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Status = SessionStatus.Pending
        };

        _mockSessionService
            .Setup(x => x.CreateSession("testuser", "testdomain"))
            .Returns(expectedSession);

        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/auth/session", 
            new StringContent(
                JsonSerializer.Serialize(new CreateSessionRequest 
                { 
                    Username = "testuser", 
                    Domain = "testdomain" 
                }),
                System.Text.Encoding.UTF8,
                "application/json"));

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var session = JsonSerializer.Deserialize<AuthSession>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        session.Should().NotBeNull();
        session!.SessionId.Should().Be(expectedSession.SessionId);
        session.AccessToken.Should().Be(expectedSession.AccessToken);
    }

    [Fact]
    public async Task CreateSession_WithoutRequest_ShouldCreateSessionWithCurrentUser()
    {
        // Arrange
        var expectedSession = new AuthSession
        {
            SessionId = "test-session-id",
            AccessToken = "test-access-token",
            MachineName = "test-machine",
            WindowsUsername = "currentuser",
            Domain = "currentdomain",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Status = SessionStatus.Pending
        };

        _mockSessionService
            .Setup(x => x.CreateSession())
            .Returns(expectedSession);

        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/auth/session", null);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var session = JsonSerializer.Deserialize<AuthSession>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        session.Should().NotBeNull();
        session!.SessionId.Should().Be(expectedSession.SessionId);
    }

    [Fact]
    public async Task GetSessionByToken_WithValidToken_ShouldReturnSession()
    {
        // Arrange
        var expectedSession = new AuthSession
        {
            SessionId = "test-session-id",
            AccessToken = "test-access-token",
            Status = SessionStatus.Approved
        };

        _mockSessionService
            .Setup(x => x.GetSessionByToken("test-access-token"))
            .Returns(expectedSession);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/auth/session/test-access-token");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var session = JsonSerializer.Deserialize<AuthSession>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        session.Should().NotBeNull();
        session!.SessionId.Should().Be(expectedSession.SessionId);
        session.Status.Should().Be(expectedSession.Status);
    }

    [Fact]
    public async Task ApproveSession_WithValidSessionId_ShouldReturnSuccess()
    {
        // Arrange
        _mockSessionService
            .Setup(x => x.ApproveSession("test-session-id"))
            .Returns(true);

        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/auth/approve/test-session-id", null);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<bool>(content);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RejectSession_WithValidSessionId_ShouldReturnSuccess()
    {
        // Arrange
        _mockSessionService
            .Setup(x => x.RejectSession("test-session-id"))
            .Returns(true);

        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/auth/reject/test-session-id", null);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<bool>(content);

        result.Should().BeTrue();
    }
}
