using RhinoMCPServer.McpHost.Session;

namespace RhinoMCPServer.McpHost.Tests.Session;

public class SessionManagerTests
{
    [Fact]
    public void GenerateSessionId_ReturnsUniqueIds()
    {
        // Arrange
        var manager = new SessionManager();
        var ids = new HashSet<string>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            ids.Add(manager.GenerateSessionId());
        }

        // Assert
        Assert.Equal(100, ids.Count); // All IDs should be unique
    }

    [Fact]
    public void GenerateSessionId_ReturnsUrlSafeString()
    {
        // Arrange
        var manager = new SessionManager();

        // Act
        var sessionId = manager.GenerateSessionId();

        // Assert
        Assert.DoesNotContain("+", sessionId);
        Assert.DoesNotContain("/", sessionId);
        Assert.DoesNotContain("=", sessionId);
    }

    [Fact]
    public void GetSession_ReturnsNull_WhenSessionDoesNotExist()
    {
        // Arrange
        var manager = new SessionManager();

        // Act
        var session = manager.GetSession("nonexistent");

        // Assert
        Assert.Null(session);
    }

    [Fact]
    public void TryGetSession_ReturnsFalse_WhenSessionDoesNotExist()
    {
        // Arrange
        var manager = new SessionManager();

        // Act
        var result = manager.TryGetSession("nonexistent", out var session);

        // Assert
        Assert.False(result);
        Assert.Null(session);
    }

    [Fact]
    public async Task RemoveSessionAsync_ReturnsFalse_WhenSessionDoesNotExist()
    {
        // Arrange
        var manager = new SessionManager();

        // Act
        var result = await manager.RemoveSessionAsync("nonexistent");

        // Assert
        Assert.False(result);
    }
}
