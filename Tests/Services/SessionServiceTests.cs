using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Core.Models;
using Moq;
using Xunit;
using FluentAssertions;

namespace ADS.WindowsAuth.Tests.Services;

/// <summary>
/// Unit tests for SessionService
/// </summary>
public class SessionServiceTests
{
    private readonly SessionService _sessionService;

    public SessionServiceTests()
    {
        _sessionService = new SessionService();
    }

    [Fact]
    public void CreateSession_ShouldReturnValidSession()
    {
        // Act
        var session = _sessionService.CreateSession();

        // Assert
        session.Should().NotBeNull();
        session.SessionId.Should().NotBeNullOrEmpty();
        session.AccessToken.Should().NotBeNullOrEmpty();
        session.MachineName.Should().NotBeNullOrEmpty();
        session.WindowsUsername.Should().NotBeNullOrEmpty();
        session.Domain.Should().NotBeNullOrEmpty();
        session.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        session.ExpiresAt.Should().BeAfter(session.CreatedAt);
        session.Status.Should().Be(SessionStatus.Pending);
    }

    [Fact]
    public void CreateSession_WithUserAndDomain_ShouldReturnValidSession()
    {
        // Arrange
        var username = "testuser";
        var domain = "testdomain";

        // Act
        var session = _sessionService.CreateSession(username, domain);

        // Assert
        session.Should().NotBeNull();
        session.SessionId.Should().NotBeNullOrEmpty();
        session.AccessToken.Should().NotBeNullOrEmpty();
        session.WindowsUsername.Should().Be(username);
        session.Domain.Should().Be(domain);
        session.Status.Should().Be(SessionStatus.Pending);
    }

    [Fact]
    public void GetSessionByToken_WithValidToken_ShouldReturnSession()
    {
        // Arrange
        var session = _sessionService.CreateSession();

        // Act
        var retrievedSession = _sessionService.GetSessionByToken(session.AccessToken);

        // Assert
        retrievedSession.Should().NotBeNull();
        retrievedSession!.SessionId.Should().Be(session.SessionId);
        retrievedSession.AccessToken.Should().Be(session.AccessToken);
    }

    [Fact]
    public void GetSessionByToken_WithInvalidToken_ShouldReturnNull()
    {
        // Act
        var session = _sessionService.GetSessionByToken("invalid-token");

        // Assert
        session.Should().BeNull();
    }

    [Fact]
    public void ApproveSession_WithValidSessionId_ShouldReturnTrue()
    {
        // Arrange
        var session = _sessionService.CreateSession();

        // Act
        var result = _sessionService.ApproveSession(session.SessionId);

        // Assert
        result.Should().BeTrue();
        
        var updatedSession = _sessionService.GetSessionByToken(session.AccessToken);
        updatedSession!.Status.Should().Be(SessionStatus.Approved);
    }

    [Fact]
    public void ApproveSession_WithInvalidSessionId_ShouldReturnFalse()
    {
        // Act
        var result = _sessionService.ApproveSession("invalid-session-id");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void RejectSession_WithValidSessionId_ShouldReturnTrue()
    {
        // Arrange
        var session = _sessionService.CreateSession();

        // Act
        var result = _sessionService.RejectSession(session.SessionId);

        // Assert
        result.Should().BeTrue();
        
        var updatedSession = _sessionService.GetSessionByToken(session.AccessToken);
        updatedSession!.Status.Should().Be(SessionStatus.Rejected);
    }

    [Fact]
    public void RejectSession_WithInvalidSessionId_ShouldReturnFalse()
    {
        // Act
        var result = _sessionService.RejectSession("invalid-session-id");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetAllSessions_ShouldReturnAllActiveSessions()
    {
        // Arrange
        var session1 = _sessionService.CreateSession();
        var session2 = _sessionService.CreateSession();

        // Act
        var allSessions = _sessionService.GetAllSessions();

        // Assert
        allSessions.Should().HaveCountGreaterOrEqualTo(2);
        allSessions.Should().Contain(s => s.SessionId == session1.SessionId);
        allSessions.Should().Contain(s => s.SessionId == session2.SessionId);
    }

    [Fact]
    public void CleanupExpiredSessions_ShouldRemoveExpiredSessions()
    {
        // Arrange
        var session = _sessionService.CreateSession();
        
        // Manually expire the session by setting expiration time in the past
        var expiredSession = _sessionService.GetSessionByToken(session.AccessToken);
        if (expiredSession != null)
        {
            // Use reflection to modify the expiration time for testing
            var expiresAtProperty = typeof(AuthSession).GetProperty(nameof(AuthSession.ExpiresAt));
            expiresAtProperty?.SetValue(expiredSession, DateTime.UtcNow.AddMinutes(-1));
        }

        // Act
        _sessionService.CleanupExpiredSessions();

        // Assert
        var cleanedSession = _sessionService.GetSessionByToken(session.AccessToken);
        cleanedSession?.Status.Should().Be(SessionStatus.Expired);
    }
}
