using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Core.Models;
using Moq;
using Xunit;
using FluentAssertions;

namespace ADS.WindowsAuth.Tests.Services;

/// <summary>
/// Unit tests for WindowsAuthService
/// </summary>
public class WindowsAuthServiceTests
{
    private readonly Mock<ILoggerService> _mockLogger;
    private readonly WindowsAuthService _windowsAuthService;

    public WindowsAuthServiceTests()
    {
        _mockLogger = new Mock<ILoggerService>();
        _windowsAuthService = new WindowsAuthService(_mockLogger.Object);
    }

    [Fact]
    public void GetCurrentWindowsUser_ShouldReturnValidUserInfo()
    {
        // Act
        var (username, domain) = _windowsAuthService.GetCurrentWindowsUser();

        // Assert
        username.Should().NotBeNullOrEmpty();
        domain.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("testuser", "password123", "testdomain")]
    [InlineData("user@domain.com", "password123", "domain")]
    public void ValidateCredentials_WithInvalidParameters_ShouldReturnFalse(
        string username, string password, string domain)
    {
        // Act
        var result = _windowsAuthService.ValidateCredentials(username, password, domain);

        // Assert
        result.Should().BeFalse();
        _mockLogger.Verify(x => x.LogWarning(It.IsAny<string>(), It.IsAny<object[]>()), Times.AtLeastOnce);
    }

    [Fact]
    public void ValidateCredentials_WithNullParameters_ShouldReturnFalse()
    {
        // Act
        var result = _windowsAuthService.ValidateCredentials(null!, null!, null!);

        // Assert
        result.Should().BeFalse();
        _mockLogger.Verify(x => x.LogWarning(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public void ValidateCredentials_WithEmptyParameters_ShouldReturnFalse()
    {
        // Act
        var result = _windowsAuthService.ValidateCredentials("", "", "");

        // Assert
        result.Should().BeFalse();
        _mockLogger.Verify(x => x.LogWarning(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once);
    }
}
