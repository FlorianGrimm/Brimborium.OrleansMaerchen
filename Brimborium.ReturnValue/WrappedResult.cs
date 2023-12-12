
namespace Brimborium.ReturnValue;

//public interface IWrappedResult<T> {
//    Result<bool> Result { get; }
//}

//public interface IWrappedOptionalResult<T> {
//    OptionalResult<bool> OptionalResult { get; }
//}

//public static class WrappedResultExtensions {
//    /*
//    public static bool TryGetValue<T>(this IWrappedResult<T> wrappedResult, [MaybeNullWhen(false)] out T value) {
//        return wrappedResult.ResultValue.TryGetValue(out value);
//    }
//    */
//}

public interface IMeaning<T> {
    T Value { get; }
}

public interface IMeaningReference<T> : IMeaning<T> {
    public bool TryGetValue([MaybeNullWhen(false)] out T value) {
        value = this.Value;
        return value is not null;
    }
}

/*
public partial record struct FilePath(string Value) : IMeaningReference<string> {
    public bool TryGetValue([MaybeNullWhen(false)] out string value) {
        value = this.Value;
        return value is not null;
    }
}
*/

public partial record struct FilePath(string Value)
    : IMeaning<string>
    , IOptionalValue<string> {
    public bool TryGetValue([MaybeNullWhen(false)] out string value) {
        value = this.Value;
        return value is not null;
    }
}

/**/