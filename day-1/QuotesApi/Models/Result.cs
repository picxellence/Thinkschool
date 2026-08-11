using System.Collections.Generic;

namespace QuotesApi.Models;

public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }
    public Dictionary<string, string[]> Errors { get; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
        Errors = new Dictionary<string, string[]>();
    }

    private Result(Dictionary<string, string[]> errors)
    {
        IsSuccess = false;
        Errors = errors;
        Value = default!;
    }

    public static Result<T> Success(T value) => new Result<T>(value);

    public static Result<T> Failure(Dictionary<string, string[]> errors) => new Result<T>(errors);
}
