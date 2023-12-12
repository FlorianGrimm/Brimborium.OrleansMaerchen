namespace Brimborium.ReturnValue;

public interface IValue<T> {
    T Value { get; }

    //void Deconstruct(out T Value);
    //bool Equals(SuccessValue<T> other);

    // SuccessValue<T> WithValue(T Value);
    // Result<T> WithError(Exception that);
}

public interface ISuccessValue<T> : IValue<T> {
}

public interface IOptionalValue<T> {
    /* unsure about IValue */
    bool TryGetValue([MaybeNullWhen(false)] out T value);
    /*
    public bool TryGetValue([MaybeNullWhen(false)] out T value) {
        value = this.Value;
        return true;
    }
    */
}

public interface IValueWithError {
    bool TryGetError([MaybeNullWhen(false)] out ErrorValue error);
}

// public interface IValueWithError<T>:IValueWithError { }

public interface IValueWithError<T, R> : IValueWithError
    where R : struct, IValue<T> {

    // bool TryGetError([MaybeNullWhen(false)] out ErrorValue error);

    bool TryGetError(
            [MaybeNullWhen(false)] out ErrorValue error,
            [MaybeNullWhen(true)] out R value);
}

public interface IOptionalValueWithError<T, R> : IValueWithError
    where R : struct, IOptionalValue<T> {

    // bool TryGetError([MaybeNullWhen(false)] out ErrorValue error);

    bool TryGetError(
            [MaybeNullWhen(false)] out ErrorValue error,
            [MaybeNullWhen(true)] out R value);
}

//public interface IWrappedValue<T> { }