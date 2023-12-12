namespace Brimborium.ReturnValue;

public static class OptionalValueExtensions {
    public static OptionalValue<T> AsOptional<T>(this T value) 
        => new(value);

    public static OptionalValue<T> If<T>(this OptionalValue<T> value, Func<T, bool> predicate) {
        if (value.TryGetValue(out var v) && predicate(v)) {
            return value;
        } else {
            return new OptionalValue<T>();
        }
    }

    public static OptionalValue<T> If<T, A>(this OptionalValue<T> value, A args, Func<T, A, bool> predicate) {
        if (value.TryGetValue(out var v) && predicate(v, args)) {
            return value;
        } else {
            return new OptionalValue<T>();
        }
    }

    public static OptionalValue<R> Map<T, A, R>(this OptionalValue<T> value, A args, Func<T, A, OptionalValue<R>> predicate) {
        if (value.TryGetValue(out var v)) {
            return predicate(v, args);
        } else {
            return NoValue.Value;
        }
    }

    public static OptionalValue<T> OrDefault<T, A>(this OptionalValue<T> value, A args, Func<A, OptionalValue<T>> fnDefaultValue) {
        if (value.TryGetValue(out var _)) {
            return value;
        } else {
            return fnDefaultValue(args);
        }
    }

    public static OptionalValue<T> OrDefault<T>(this OptionalValue<T> value, OptionalValue<T> defaultValue) {
        if (value.TryGetValue(out var _)) {
            return value;
        } else {
            return defaultValue;
        }
    }
}