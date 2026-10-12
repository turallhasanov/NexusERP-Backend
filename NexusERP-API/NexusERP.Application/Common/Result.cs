namespace NexusERP.Application.Common;

public enum ErrorType
{
    Validation,
    Unauthorized,
    NotFound,
    Conflict
}

/// <summary>Outcome of an application service call: either a response body or an Azerbaijani error message.</summary>
public sealed class Result<T>
{
    private Result(T? value, ErrorType errorType, string? error)
    {
        Value = value;
        ErrorType = errorType;
        Error = error;
    }

    public T? Value { get; }
    public ErrorType ErrorType { get; }
    public string? Error { get; }
    public bool IsSuccess => Error is null;

    public static Result<T> Success(T value) => new(value, default, null);
    public static Result<T> Failure(ErrorType errorType, string error) => new(default, errorType, error);

    public static implicit operator Result<T>(T value) => Success(value);
}
