namespace Brimborium.ReturnValue;

public record struct ValidatedValue<T>(OptionalValue<T> Value) : IValue<OptionalValue<T>> {
    //T IValue<T>.Value => throw new NotImplementedException();

    //public bool TryGetValue([MaybeNullWhen(false)] out T value) {
    //    throw new NotImplementedException();
    //}
    public bool TryGetValue([MaybeNullWhen(false)] out OptionalValue<T> value) {
        throw new NotImplementedException();
    }
}
