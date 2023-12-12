namespace Brimborium.ReturnValue;

public static class OptionalResultExtensions {
    public static Results<Ok<T>, NotFound> ToGetResults<T>(this OptionalResult<T> optional) {
        if (optional.TryGetValue(out var value)) {
            return TypedResults.Ok<T>(value);
        } else if (optional.TryGetNoValue()) {
            return TypedResults.NotFound();
        } else if (optional.TryGetError(out var errorValue)) {
            errorValue.Throw();
        } 
        throw new InvalidOperationException();
    }
    /*
    public static Results<Ok, NotFound> ToPostResults<T>(this OptionalResult<T> optional) {
        if (optional.TryGetValue(out var value)) {
            return TypedResults.Ok<T>(value);
        } else if (optional.TryGetNoValue()) {
            return TypedResults.NotFound();
        } else if (optional.TryGetError(out var errorValue)) {
            errorValue.Throw();
        }
        throw new InvalidOperationException();
    }
    */
}
