# C# Design Principles

## Overview

This document outlines the C# design philosophy for this project, drawing inspiration from three influential software engineers and their distinct approaches to code quality and architecture.

## Core Design Philosophy

### John Carmack's Approach: Functional Clarity & Performance

_"The idea that I can be presented with a problem, set out to logically solve it with the tools at hand, and wind up with a program that could not be implemented in any other language."_

**Principles:**

- **Pure Functions First**: Favor stateless, predictable methods over stateful classes
- **Explicit Dependencies**: Make all dependencies visible in constructors and method signatures
- **Minimal Abstractions**: Only abstract when complexity reduction is clear and measurable
- **Performance Awareness**: Consider memory allocation, GC pressure, and execution paths

**Implementation:**

```csharp
// Good: Pure function with explicit dependencies
public static class ProjectCalculator
{
    public static int CalculateProjectProgress(IReadOnlyList<Process> processes)
    {
        if (processes is null || processes.Count == 0)
        {
            return 0;
        }

        var completed = processes.Count(p => p.Progress == 100);
        return (int)Math.Round((double)completed / processes.Count * 100);
    }
}

// Avoid: Hidden state and side effects
public class ProjectManager
{
    private readonly Dictionary<Guid, object> _cachedStats = new(); // Hidden state

    public void UpdateProject(Project project)
    {
        // ... side effects buried in methods
    }
}
```

### Robert C. Martin's Approach: Clean Architecture & SOLID

_"Clean code is simple and direct. Clean code reads like well-written prose."_

**Principles:**

- **Single Responsibility**: Each class/method has one reason to change
- **Dependency Inversion**: Depend on abstractions, not concretions
- **Clear Naming**: Names should reveal intent without comments
- **Small Methods**: Methods should do one thing well

**Implementation:**

```csharp
// Good: Interface-based abstraction
public interface IMCPTool
{
    /// <summary>
    /// MCPツールの実行処理を担当
    ///
    /// 責務:
    /// - パラメータの検証
    /// - Rhino APIの呼び出し
    /// - 結果の構造化
    /// </summary>
    string Name { get; }
    string Description { get; }
    string InputSchema { get; }
    Task<CallToolResult> ExecuteAsync(JsonElement parameters);
}

// Good: Dependency inversion through constructor injection
public class ToolManager
{
    private readonly ILogger _logger;
    private readonly IToolPluginLoader _pluginLoader;

    public ToolManager(ILogger logger, IToolPluginLoader pluginLoader)
    {
        _logger = logger;
        _pluginLoader = pluginLoader;
    }
}
```

### Rob Pike's Approach: Simplicity & Composition

_"Simplicity is the ultimate sophistication. Clear is better than clever."_

**Principles:**

- **Composition over Inheritance**: Build complex behavior by combining simple parts
- **Explicit over Implicit**: Make behavior obvious at the call site
- **Minimal Interfaces**: Keep public APIs small and focused
- **Readable Code**: Code should tell a story without extensive documentation

**Implementation:**

```csharp
// Good: Simple composition with interfaces
public class McpHostRunner
{
    private readonly IToolExecutor _toolExecutor;
    private readonly IMcpServer _server;

    public McpHostRunner(IToolExecutor toolExecutor, IMcpServer server)
    {
        _toolExecutor = toolExecutor;
        _server = server;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await _server.StartAsync(cancellationToken);
    }
}

// Avoid: Complex inheritance hierarchies
public abstract class BaseToolHandler
{
    protected abstract void OnExecute();
}

public class ComplexToolHandler : BaseToolHandler
{
    // ... complex inheritance chain
}
```

## Project-Specific Guidelines

### 1. MCP Tool Implementation

**Always implement `IMCPTool` interface** for new tools:

```csharp
public class CreateSphereTool : IMCPTool
{
    public string Name => "create_sphere";

    public string Description => "Creates a sphere at the specified location";

    public string InputSchema => """
        {
            "type": "object",
            "properties": {
                "center": {
                    "type": "object",
                    "properties": {
                        "x": { "type": "number" },
                        "y": { "type": "number" },
                        "z": { "type": "number" }
                    },
                    "required": ["x", "y", "z"]
                },
                "radius": { "type": "number" }
            },
            "required": ["center", "radius"]
        }
        """;

    public async Task<CallToolResult> ExecuteAsync(JsonElement parameters)
    {
        // Validate parameters first
        if (!parameters.TryGetProperty("center", out var center))
        {
            return CreateErrorResult("center is required");
        }

        // Execute Rhino operations
        var result = await RhinoApp.InvokeOnUiThreadAsync(() =>
        {
            // ... Rhino API calls
        });

        return CreateSuccessResult(result);
    }
}
```

### 2. AssemblyLoadContext Isolation

**Critical**: Use `AssemblyLoadContext` to isolate MCP SDK from Rhino runtime:

```csharp
// Good: Isolated context for SDK dependencies
public class McpLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public McpLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
    }
}
```

**Key principle**: Rhino 8 uses System.Text.Json 8.x, MCP SDK requires 10.x. Isolation prevents version conflicts.

### 3. Async/Await Patterns

**Follow these patterns for async operations:**

```csharp
// Good: Proper async pattern with cancellation
public async Task<Result> ExecuteAsync(CancellationToken cancellationToken = default)
{
    await Task.Yield(); // Ensure truly async

    return await RhinoApp.InvokeOnUiThreadAsync(() =>
    {
        // UI thread operations
    });
}

// Good: ConfigureAwait for library code
public async Task<byte[]> ReadFileAsync(string path)
{
    return await File.ReadAllBytesAsync(path).ConfigureAwait(false);
}

// Avoid: Blocking on async code
public Result Execute()
{
    return ExecuteAsync().Result; // ❌ Deadlock risk
}
```

### 4. Nullable Reference Types

**Enable and respect nullable annotations:**

```csharp
// Good: Explicit nullability
public class ToolResult
{
    public string Message { get; init; } = string.Empty;
    public object? Data { get; init; }
    public string? ErrorDetails { get; init; }
}

// Good: Guard clauses for nullable parameters
public static void ProcessTool(IMCPTool? tool)
{
    ArgumentNullException.ThrowIfNull(tool);
    // ... process
}

// Avoid: Ignoring nullable warnings with !
var value = possiblyNull!.Property; // ❌ Suppressing warnings
```

### 5. Error Handling

- **Explicit Error States**: Use Result pattern or exceptions appropriately
- **Fail Fast**: Return early on invalid states
- **Meaningful Messages**: Error messages should guide user action

```csharp
public record ToolResult
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public JsonElement? Data { get; init; }

    public static ToolResult Success(string message, object? data = null) =>
        new() { IsSuccess = true, Message = message, Data = JsonSerializer.SerializeToElement(data) };

    public static ToolResult Failure(string message) =>
        new() { IsSuccess = false, Message = message };
}
```

### 6. JSON Schema Definition

**Define schemas as raw string literals:**

```csharp
public string InputSchema => """
    {
        "type": "object",
        "properties": {
            "objectId": {
                "type": "string",
                "description": "The GUID of the Rhino object"
            },
            "layerName": {
                "type": "string",
                "description": "Target layer name"
            }
        },
        "required": ["objectId"]
    }
    """;
```

### 7. Module Organization

- **Feature-Based Structure**: Group related functionality together (Tools/, Common/, McpHost/)
- **Clear Boundaries**: Each project should have a clear, single purpose
- **Minimal Public Surface**: Use `internal` by default, `public` only when necessary
- **Consistent Naming**: Use Japanese comments for business logic documentation

## Decision Framework

When designing new functionality, ask:

1. **Carmack**: Is this the simplest solution that works? Can I test this easily?
2. **Martin**: Does this have a single responsibility? Are dependencies clear?
3. **Pike**: Can a new team member understand this immediately? Is composition used over inheritance?

## Anti-Patterns to Avoid

- **God Classes**: Classes that do too many things
- **Hidden Dependencies**: Dependencies not visible in constructors/parameters
- **Deep Inheritance**: Prefer composition and interfaces
- **Clever Code**: Code that requires comments to understand
- **Premature Abstraction**: Creating abstractions before patterns emerge
- **Service Locator**: Avoid hidden global state; use explicit DI instead

## Testing Philosophy

- **Unit Tests for Pure Functions**: Test business logic in isolation
- **Integration Tests for Tools**: Test tool execution with mocked Rhino context
- **Mock External Dependencies**: Keep tests fast and reliable
- **Test Behavior, Not Implementation**: Focus on outcomes, not internal details

## Code Quality Tools

### Linting & Formatting

**必須実行**: コード変更後は必ずビルドと警告チェックを実行する

```bash
# ビルドと警告確認
dotnet build --warnaserror

# フォーマット確認
dotnet format --verify-no-changes

# フォーマット適用
dotnet format
```

**実行タイミング**:

- ファイル編集後
- コミット前
- プルリクエスト作成前

---

_"The best code is no code at all. The second best code is simple, readable, and does exactly what it needs to do."_
