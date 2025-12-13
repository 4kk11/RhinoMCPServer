namespace RhinoMCPServer.McpHost.Results;

/// <summary>
/// Generic Result type for operations that can fail.
/// Follows Carmack's principle of explicit error states.
/// </summary>
/// <typeparam name="T">The type of the value on success</typeparam>
public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }

    private Result(bool isSuccess, T? value, string? errorMessage, Exception? exception)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    /// <summary>
    /// Creates a successful result with the given value.
    /// </summary>
    public static Result<T> Success(T value) =>
        new(true, value, null, null);

    /// <summary>
    /// Creates a failed result with the given error message.
    /// </summary>
    public static Result<T> Failure(string errorMessage) =>
        new(false, default, errorMessage, null);

    /// <summary>
    /// Creates a failed result with the given error message and exception.
    /// </summary>
    public static Result<T> Failure(string errorMessage, Exception exception) =>
        new(false, default, errorMessage, exception);

    /// <summary>
    /// Pattern matches on the result, calling onSuccess if successful or onFailure if failed.
    /// </summary>
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value!) : onFailure(ErrorMessage!);

    /// <summary>
    /// Executes an action based on the result state.
    /// </summary>
    public void Match(
        Action<T> onSuccess,
        Action<string> onFailure)
    {
        if (IsSuccess)
            onSuccess(Value!);
        else
            onFailure(ErrorMessage!);
    }
}

/// <summary>
/// Unit type for Result operations that don't return a value.
/// </summary>
public readonly struct Unit
{
    public static Unit Value { get; } = new();
}
