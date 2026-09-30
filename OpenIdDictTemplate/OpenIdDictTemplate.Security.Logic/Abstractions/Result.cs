namespace OpenIdDictTemplate.Security.Logic.Abstractions;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    Unexpected
}

public record Error(ErrorType Type, string Message);

public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public static Result Ok() => new(null);

    public static Result Fail(ErrorType type, string message) => new(new Error(type, message));

    public static Result<T> Ok<T>(T value) => new(value, null);

    public static Result<T> Fail<T>(ErrorType type, string message) => new(default, new Error(type, message));
}

public class Result<T> : Result
{
    internal Result(T? value, Error? error) : base(error) => Value = value;

    public T? Value { get; }
}
