namespace Brimborium.ReturnValue;

[global::Orleans.GenerateSerializer]
[global::Orleans.Immutable]
public record NoValue {
    public NoValue() { }

    public static NoValue Value => new NoValue();

    public override string ToString() => string.Empty;

#pragma warning disable CA1822 // Mark members as static
    public OptionalValue<T> ToOptional<T>() => new OptionalValue<T>();

    public SuccessValue<T> WithValue<T>(T value) => new SuccessValue<T>(value);
#pragma warning restore CA1822 // Mark members as static
}
