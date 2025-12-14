using RhinoMCPServer.McpHost.Results;

namespace RhinoMCPServer.McpHost.Tests.Results;

public class ResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void Failure_CreatesFailedResultWithMessage()
    {
        var result = Result<int>.Failure("Something went wrong");

        Assert.False(result.IsSuccess);
        Assert.Equal(default, result.Value);
        Assert.Equal("Something went wrong", result.ErrorMessage);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void Failure_CreatesFailedResultWithException()
    {
        var exception = new InvalidOperationException("Test exception");
        var result = Result<int>.Failure("Operation failed", exception);

        Assert.False(result.IsSuccess);
        Assert.Equal(default, result.Value);
        Assert.Equal("Operation failed", result.ErrorMessage);
        Assert.Same(exception, result.Exception);
    }

    [Fact]
    public void Match_ReturnsOnSuccessResultWhenSuccessful()
    {
        var result = Result<int>.Success(42);

        var output = result.Match(
            onSuccess: value => $"Value: {value}",
            onFailure: error => $"Error: {error}");

        Assert.Equal("Value: 42", output);
    }

    [Fact]
    public void Match_ReturnsOnFailureResultWhenFailed()
    {
        var result = Result<int>.Failure("Something went wrong");

        var output = result.Match(
            onSuccess: value => $"Value: {value}",
            onFailure: error => $"Error: {error}");

        Assert.Equal("Error: Something went wrong", output);
    }

    [Fact]
    public void Match_Action_CallsOnSuccessWhenSuccessful()
    {
        var result = Result<int>.Success(42);
        var successCalled = false;
        var failureCalled = false;

        result.Match(
            onSuccess: _ => successCalled = true,
            onFailure: _ => failureCalled = true);

        Assert.True(successCalled);
        Assert.False(failureCalled);
    }

    [Fact]
    public void Match_Action_CallsOnFailureWhenFailed()
    {
        var result = Result<int>.Failure("Error");
        var successCalled = false;
        var failureCalled = false;

        result.Match(
            onSuccess: _ => successCalled = true,
            onFailure: _ => failureCalled = true);

        Assert.False(successCalled);
        Assert.True(failureCalled);
    }

    [Fact]
    public void Success_WithReferenceType_WorksCorrectly()
    {
        var result = Result<string>.Success("hello");

        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void Failure_WithReferenceType_HasNullValue()
    {
        var result = Result<string>.Failure("Error");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
    }
}
