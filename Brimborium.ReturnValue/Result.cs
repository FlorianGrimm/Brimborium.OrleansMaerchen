namespace Brimborium.ReturnValue;

public static class Result {
    public static NoValue NoValue => new NoValue();

    public static SuccessValue<T> AsSuccessValue<T>(this T that)
        => new SuccessValue<T>(that);

    public static ErrorValue AsErrorValue(this Exception that)
        => new ErrorValue(that);

    public static Result<T> AsResult<T>(this T that)
        => new Result<T>(that);

    public static Result<T> AsResult<T>(this SuccessValue<T> that)
        => new Result<T>(that.Value);

    public static Result<T> AsResult<T>(this OptionalResult<T> that) {
        if (that.TryGetValue(out var successValue)) {
            return new Result<T>(successValue);
        } else if (that.TryGetError(out var errorValue)) {
            return new Result<T>(errorValue);
        }
        return new Result<T>(new UninitializedException());
    }

    public static Result<T> AsResult<T>(this Exception that)
        => new Result<T>(that);

    public static Result<T> AsResult<T>(this ErrorValue that)
        => new Result<T>(that);


    public static OptionalResult<T> AsOptionalResult<T>(this NoValue that)
        => new OptionalResult<T>();

    public static OptionalResult<T> AsOptionalResult<T>(this T that)
        => new OptionalResult<T>(that);

    public static OptionalResult<T> AsOptionalResult<T>(this SuccessValue<T> that)
        => new OptionalResult<T>(that.Value);

    public static OptionalResult<T> AsOptionalResult<T>(this Exception that)
        => new OptionalResult<T>(that);

    public static OptionalResult<T> AsOptionalResult<T>(this ErrorValue value)
        => new OptionalResult<T>(value);

    public static OptionalResult<T> AsOptionalResult<T>(this Result<T> that) {
        if (that.TryGetValue(out var successValue)) {
            return new OptionalResult<T>(successValue);
        } else if (that.TryGetError(out var errorValue)) {
            return new OptionalResult<T>(errorValue);
        } else {
            return new OptionalResult<T>(new InvalidEnumArgumentException($"Invalid enum {that.Mode}."));
        }
    }

    public static Result<T> TryCatch<A, T>(this A arg, Func<A, T> fn) {
        try {
            return fn(arg);
        } catch (Exception error) {
            return new Result<T>(error);
        }
    }

    public static async Task<OptionalResult<T>> TryCatchAsync<A, T>(
        this A arg,
        Func<A, Task<OptionalResult<T>>> fn) {
        try {
            return await fn(arg);
        } catch (Exception error) {
            return ErrorValue.CreateFromCatchedException(error);
        }
    }
    public static async Task<R> ChainAsync<T, R>(
        this Task<T> futureT,
        Func<T, R> map
        ) {
        var value = await futureT;
        return map(value);
    }
}
