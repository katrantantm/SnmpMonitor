using System;
using System.Collections.Generic;
using System.Linq;

namespace SnmpMonitor.Results
{
    /// <summary>
    /// Represents the result of an operation that can either succeed or fail
    /// </summary>
    public class Result<T>
    {
        public bool IsSuccess { get; }
        public T? Value { get; }
        public string? Error { get; }
        public Exception? Exception { get; }

        private Result(bool isSuccess, T? value, string? error, Exception? exception)
        {
            IsSuccess = isSuccess;
            Value = value;
            Error = error;
            Exception = exception;
        }

        public static Result<T> Success(T value) => new(true, value, null, null);
        public static Result<T> Failure(string error) => new(false, default, error, null);
        public static Result<T> Failure(Exception exception) => new(false, default, exception.Message, exception);

        public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure) =>
            IsSuccess ? onSuccess(Value!) : onFailure(Error!);

        public T GetValueOrDefault(T defaultValue = default!) => IsSuccess ? Value! : defaultValue;
    }

    /// <summary>
    /// Non-generic result for operations without return value
    /// </summary>
    public class Result
    {
        public bool IsSuccess { get; }
        public string? Error { get; }
        public Exception? Exception { get; }

        private Result(bool isSuccess, string? error, Exception? exception)
        {
            IsSuccess = isSuccess;
            Error = error;
            Exception = exception;
        }

        public static Result Success() => new(true, null, null);
        public static Result Failure(string error) => new(false, error, null);
        public static Result Failure(Exception exception) => new(false, exception.Message, exception);
    }
}
