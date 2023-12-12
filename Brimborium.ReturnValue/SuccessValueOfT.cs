namespace Brimborium.ReturnValue;

[global::Orleans.GenerateSerializer]
[global::Orleans.Immutable]
public record struct SuccessValue<T>(T Value) 
    : IValue<T> 
    , ISuccessValue<T>
    {
    public readonly SuccessValue<T> WithValue(T Value) => new SuccessValue<T>(Value);

    public readonly Result<T> WithError(Exception that) => new Result<T>(new ErrorValue(that));

    public bool TryGetValue([MaybeNullWhen(false)] out T value) {
        value = this.Value;
        return true;
    }

    public static implicit operator SuccessValue<T>(T that) => new SuccessValue<T>(that);

    public static implicit operator T(SuccessValue<T> that) => that.Value;
}
