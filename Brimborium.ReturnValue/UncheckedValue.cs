namespace Brimborium.ReturnValue;

[global::Orleans.GenerateSerializer]
[global::Orleans.Immutable]
public record struct UncheckedValue<T>(T Value) : IValue<T> {
    public readonly UncheckedValue<T> WithValue(T Value) => new UncheckedValue<T>(Value);

    public readonly Result<T> WithError(Exception that) => new Result<T>(new ErrorValue(that));

    public bool TryGetValue([MaybeNullWhen(false)] out T value) {
        value = this.Value;
        return true;
    }

    public static implicit operator UncheckedValue<T>(T that) => new UncheckedValue<T>(that);

    public static implicit operator T(UncheckedValue<T> that) => that.Value;
}

public static class UncheckedValueExtension {
    public static UncheckedValue<T> AsUncheckedValue<T>(this T value) => new UncheckedValue<T>(value);
}