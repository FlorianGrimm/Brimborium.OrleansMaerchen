namespace Brimborium.ReturnValue;

public enum ResultMode { Success, Error }

[global::Orleans.GenerateSerializer]
[global::Orleans.Immutable]
public struct Result<T> {
    public readonly ResultMode Mode;
    [AllowNull] public readonly T Value;
    [AllowNull] public readonly ErrorValue Error;

    public Result() {
        Mode = ResultMode.Error;
        Value = default(T);
        Error = ErrorValue.Uninitialized;
    }

    public Result(T Value) {
        this.Mode = ResultMode.Success;
        this.Value = Value;
        this.Error = default;
    }

    public Result(ErrorValue error) {
        this.Mode = ResultMode.Error;
        this.Value = default;
        this.Error = error;
    }

    public void Deconstruct(out ResultMode mode, out T? value, out OptionalErrorValue error) {
        mode = this.Mode;

        if (this.Mode == ResultMode.Success) {
            value = this.Value;
            error = default;
        } else {
            value = default;
            error = this.Error;
        }
    }

    public bool TryGet(
        [MaybeNullWhen(false)] out T value,
        [MaybeNullWhen(true)] out ErrorValue error) {
        if (this.Mode == ResultMode.Success) {
            value = this.Value!;
            error = default;
            return true;
        } else {
            value = default;
            error = this.Error!;
            return false;
        }
    }

    public bool TryGetValue([MaybeNullWhen(false)] out T value) {
        if (this.Mode == ResultMode.Success) {
            value = this.Value!;
            return true;
        } else {
            value = default;
            return false;
        }
    }

    public bool TryGetError([MaybeNullWhen(false)] out ErrorValue error) {
        if (this.Mode == ResultMode.Error) {
            error = this.Error!;
            return true;
        } else {
            error = default;
            return false;
        }
    }

    public bool TryGetError(
        [MaybeNullWhen(false)] out ErrorValue error,
        [MaybeNullWhen(true)] out SuccessValue<T> value) {
        if (this.Mode == ResultMode.Error) {
            error = this.Error!;
            value = default;
            return true;
        } else {
            error = default;
            value = new SuccessValue<T>(this.Value!);
            return false;
        }
    }

    public Result<T> WithValue(T value) => new Result<T>(value);

    public Result<T> WithError(ErrorValue error) => new Result<T>(error);

    public static implicit operator Result<T>(T value) => new Result<T>(value);
    public static implicit operator Result<T>(Exception error) => new Result<T>(new ErrorValue(error, null, false));

    public static implicit operator Result<T>(SuccessValue<T> successValue) => new Result<T>(successValue);
    public static implicit operator Result<T>(ErrorValue errorValue) => new Result<T>(errorValue);
}
