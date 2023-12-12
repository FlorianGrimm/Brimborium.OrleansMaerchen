namespace Brimborium.ReturnValue;

public enum OptionalResultMode { NoValue, Success, Error }

[global::Orleans.GenerateSerializer]
[global::Orleans.Immutable]
public struct OptionalResult<T>
    : IValue<T>
    , IOptionalValueWithError<T, OptionalValue<T>>
    {
    [JsonInclude]
    [global::Orleans.Id(0)]
    public readonly OptionalResultMode Mode;
    [JsonInclude]
    [global::Orleans.Id(1)]
    [AllowNull] public readonly T Value;
    [JsonInclude]
    [global::Orleans.Id(2)]
    [AllowNull] public readonly ErrorValue Error;

    T IValue<T>.Value => this.Value;

    public OptionalResult() {
        this.Mode = OptionalResultMode.NoValue;
        this.Value = default;
        this.Error = default;
    }

    public OptionalResult(T Value) {
        this.Mode = OptionalResultMode.Success;
        this.Value = Value;
        this.Error = default;
    }

    public OptionalResult(ErrorValue error) {
        this.Mode = OptionalResultMode.Error;
        this.Value = default;
        this.Error = error;
    }

    [JsonConstructor]
    [global::Orleans.OrleansConstructor()]
    public OptionalResult(OptionalResultMode mode, [AllowNull] T value, [AllowNull] ErrorValue error) {
        this.Mode = mode;
        this.Value = value;
        this.Error = error;
    }

    // TODO: better?    OptionalErrorValue
    public readonly void Deconstruct(out OptionalResultMode mode, out T? value, out OptionalErrorValue error) {
        switch (this.Mode) {
            case OptionalResultMode.Success:
                mode = OptionalResultMode.Success;
                value = this.Value;
                error = default;
                return;
            case OptionalResultMode.Error:
                mode = OptionalResultMode.Error;
                value = default;
                error = this.Error;
                return;
            default:
                mode = OptionalResultMode.NoValue;
                value = default;
                error = default;
                return;
        }
    }

    public readonly bool TryGetNoValue() {
        if (this.Mode == OptionalResultMode.NoValue) {
            return true;
        } else if (this.Mode == OptionalResultMode.Success) {
            return false;
        } else if (this.Mode == OptionalResultMode.Error) {
            return false;
        } else {
            return true;
        }
    }

    public readonly bool TryGetNoValue([MaybeNullWhen(true)] out T value) {
        if (this.Mode == OptionalResultMode.Success) {
            value = this.Value!;
            return false;
        } else {
            value = default;
            return true;
        }
    }

    public readonly bool TryGetValue([MaybeNullWhen(false)] out T value) {
        if (this.Mode == OptionalResultMode.Success) {
            value = this.Value!;
            return true;
        } else {
            value = default;
            return false;
        }
    }

    public readonly bool TryGetError([MaybeNullWhen(false)] out ErrorValue error) {
        if (this.Mode == OptionalResultMode.Error) {
            System.Diagnostics.Debug.Assert(this.Error.Exception is not null);
            error = this.Error!;
            return true;
        } else {
            error = default;
            return false;
        }
    }

    public readonly bool TryGetError(
        [MaybeNullWhen(false)] out ErrorValue error, 
        [MaybeNullWhen(true)] out OptionalValue<T> value) {
        if (this.Mode == OptionalResultMode.Error) {
            error = this.Error!;
            value = default;
            return true;
        } else if (this.Mode == OptionalResultMode.Success){
            error = default;
            value = new OptionalValue<T>(this.Value);
            return false;
        } else {
            error = default;
            value = new OptionalValue<T>();
            return false;
        }
    }

    public readonly OptionalResult<T> WithNoValue() => new OptionalResult<T>();

    public readonly OptionalResult<T> WithValue(T value) => new OptionalResult<T>(value);

    public readonly OptionalResult<T> WithError(ErrorValue error) => new OptionalResult<T>(error);


    public static implicit operator bool(OptionalResult<T> that) => that.Mode == OptionalResultMode.Success;

    public static bool operator true(OptionalResult<T> that) => that.Mode == OptionalResultMode.Success;
    
    public static bool operator false(OptionalResult<T> that) => that.Mode != OptionalResultMode.Success;
    
    public static explicit operator T(OptionalResult<T> that) => (that.Mode == OptionalResultMode.Success) ? that.Value : throw new InvalidCastException();
    
    public static explicit operator ErrorValue(OptionalResult<T> that) => (that.Mode == OptionalResultMode.Error) ? that.Error : throw new InvalidCastException();

    public static implicit operator OptionalResult<T>(NoValue noValue) => new OptionalResult<T>();

    public static implicit operator OptionalResult<T>(T value) => new OptionalResult<T>(value);

    public static implicit operator OptionalResult<T>(Exception error) => new OptionalResult<T>(new ErrorValue(error));

    public static implicit operator OptionalResult<T>(ErrorValue error) => new OptionalResult<T>(error);

    public static implicit operator OptionalResult<T>(Result<T> value) {
        if (value.TryGetValue(out var successValue)) {
            return new OptionalResult<T>(successValue);
        } else if (value.TryGetError(out var errorValue)) {
            return new OptionalResult<T>(errorValue);
        } else {
            return new OptionalResult<T>(new InvalidEnumArgumentException($"Invalid enum {value.Mode}."));
        }
    }
}
