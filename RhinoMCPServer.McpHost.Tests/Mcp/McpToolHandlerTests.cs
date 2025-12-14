using RhinoMCPServer.McpHost.Mcp;

namespace RhinoMCPServer.McpHost.Tests.Mcp;

public class McpToolHandlerTests
{
    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenToolExecutorIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new McpToolHandler(null!));
    }

    [Fact]
    public async Task HandleListToolsAsync_ReturnsEmptyList_WhenNoTools()
    {
        // Arrange
        var mockExecutor = new MockToolExecutor("[]");
        var handler = new McpToolHandler(mockExecutor);

        // Act
        var result = await handler.HandleListToolsAsync(null!, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Tools);
    }

    [Fact]
    public async Task HandleListToolsAsync_ReturnsTools_WhenToolsExist()
    {
        // Arrange
        var toolsJson = """
            [
                {
                    "Name": "test_tool",
                    "Description": "A test tool",
                    "InputSchemaJson": "{\"type\":\"object\"}"
                }
            ]
            """;
        var mockExecutor = new MockToolExecutor(toolsJson);
        var handler = new McpToolHandler(mockExecutor);

        // Act
        var result = await handler.HandleListToolsAsync(null!, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Tools);
        Assert.Equal("test_tool", result.Tools[0].Name);
        Assert.Equal("A test tool", result.Tools[0].Description);
    }

    private class MockToolExecutor : IToolExecutor
    {
        private readonly string _listToolsJson;
        private readonly string _executeResultJson;

        public MockToolExecutor(string listToolsJson, string executeResultJson = "{\"IsError\":false,\"Contents\":[]}")
        {
            _listToolsJson = listToolsJson;
            _executeResultJson = executeResultJson;
        }

        public string ListToolsJson() => _listToolsJson;

        public Task<string> ExecuteToolJsonAsync(string toolName, string argumentsJson)
            => Task.FromResult(_executeResultJson);
    }
}
