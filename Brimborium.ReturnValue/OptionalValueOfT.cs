namespace Brimborium.ReturnValue;

public enum OptionalMode { NoValue, Success }

[global::Orleans.GenerateSerializer]
[global::Orleans.Immutable]
public readonly struct OptionalValue<T>
    :IOptionalValue<T>
{
    [JsonInclude]
    [global::Orleans.Id(0)]
    public readonly OptionalMode Mode;

    [EditorBrowsable(EditorBrowsableState.Never)]
    [JsonInclude]
    [global::Orleans.Id(1)]
    [AllowNull]
    public readonly T Value;

    public OptionalValue()
    {
        this.Mode = OptionalMode.NoValue;
        this.Value = default;
    }

    public OptionalValue(
        T value
    )
    {
        this.Mode = OptionalMode.Success;
        this.Value = value;
    }

    [JsonConstructor]
    public OptionalValue(
        OptionalMode Mode,
        [AllowNull]
        T Value
    )
    {
        if (Mode == OptionalMode.Success) {
            this.Mode = OptionalMode.Success;
            this.Value = Value;
        } else { 
            this.Mode = OptionalMode.NoValue;
            this.Value = Value;
        }
    }

    public void Deconstruct(out OptionalMode mode, [AllowNull] out T value) {
        mode = this.Mode;
        value = this.Value;
    }

    public bool TryGetNoValue()
    {
        return (this.Mode == OptionalMode.NoValue);
    }

    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        if (this.Mode == OptionalMode.NoValue)
        {
            value = default;
            return false;
        } else
        {
            value = this.Value!;
            return true;
        }
    }

    public T GetValueOrDefault(T defaultValue)
        => (this.Mode == OptionalMode.NoValue)
        ? defaultValue
        : this.Value!;


#pragma warning disable IDE0060 // Remove unused parameter
    public static implicit operator OptionalValue<T>(NoValue value) => new OptionalValue<T>();
#pragma warning restore IDE0060 // Remove unused parameter

    public static implicit operator OptionalValue<T>(T value) => new OptionalValue<T>(value);

    public static implicit operator bool(OptionalValue<T> that) => that.Mode == OptionalMode.Success;
    public static bool operator true(OptionalValue<T> that) => that.Mode == OptionalMode.Success;
    public static bool operator false(OptionalValue<T> that) => that.Mode != OptionalMode.Success;
    
    public static explicit operator T(OptionalValue<T> that) => (that.Mode == OptionalMode.Success) ? that.Value : throw new InvalidCastException();

}
