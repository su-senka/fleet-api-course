using System.Diagnostics.CodeAnalysis;

namespace Fleet.Common.Results;

/// <summary>
/// The outcome of an operation that either succeeded or failed with an <see cref="Results.Error"/>.
/// </summary>
/// <remarks>
/// <para>
/// Application services return <c>Result</c> instead of throwing, because "this vehicle is already
/// booked" is an expected outcome of a booking service, not an exceptional one. Exceptions stay
/// for genuine faults: a dropped connection, a bug, a violated invariant.
/// </para>
/// <para>
/// This is a class rather than a struct on purpose. A struct would allow <c>default(Result)</c>,
/// which would be neither a success nor a well-formed failure - exactly the sort of trap that
/// wastes an afternoon.
/// </para>
/// </remarks>
public class Result
{
    protected Result(Error? error) => Error = error;

    /// <summary>The failure, or <c>null</c> when the operation succeeded.</summary>
    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => Error is not null;

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error);

    /// <summary>Lets a service write <c>return Error.NotFound(...);</c> directly.</summary>
    public static implicit operator Result(Error error) => Failure(error);

    /// <summary>
    /// Collapses both branches into a single value. This is the method the API layer uses to turn
    /// a result into an <c>IResult</c> without ever touching <c>Value</c> unguarded.
    /// </summary>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure) =>
        IsSuccess ? onSuccess() : onFailure(Error);
}

/// <summary>
/// The outcome of an operation that produces a <typeparamref name="T"/> when it succeeds.
/// </summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T? value, Error? error) : base(error) => _value = value;

    /// <summary>
    /// The produced value.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the result is a failure. Check <see cref="Result.IsSuccess"/> first, or use
    /// <see cref="Match{TOut}(Func{T,TOut},Func{Error,TOut})"/> and avoid the question entirely.
    /// </exception>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            $"Cannot read Value of a failed Result<{typeof(T).Name}>. The error was: {Error}");

    public static Result<T> Success(T value) => new(value, null);

    public static new Result<T> Failure(Error error) => new(default, error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(Error);

    /// <summary>Transforms a successful value, passing any failure through untouched.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> map) =>
        IsSuccess ? Result<TOut>.Success(map(_value!)) : Result<TOut>.Failure(Error);
}
