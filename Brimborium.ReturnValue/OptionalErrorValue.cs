#pragma warning disable IDE0031 // Use null propagation

namespace Brimborium.ReturnValue;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
[global::Orleans.GenerateSerializer(GenerateFieldIds = Orleans.GenerateFieldIds.None, IncludePrimaryConstructorParameters = false)]
[global::Orleans.Immutable]
public record struct OptionalErrorValue(
    [property:global::Orleans.Id(0)]
    Exception? Exception = default,
    ExceptionDispatchInfo? ExceptionDispatchInfo = default,
    [property:global::Orleans.Id(1)]
    bool IsLogged = false
    ) {

    public bool TryGetError([MaybeNullWhen(false)] out Exception error) { 
        if (this.Exception is not null) {
            error = this.Exception;
            return true;
        } else {
            error = default;
            return false;
        }
    }
    
    public readonly void Throw() {
        if (this.ExceptionDispatchInfo is not null) {
            this.ExceptionDispatchInfo.Throw();
        }

        if (this.Exception is not null) {
            throw this.Exception;
        }
    }

    public readonly OptionalErrorValue WithIsLogged(bool isLogged = true)
        => new OptionalErrorValue(this.Exception, this.ExceptionDispatchInfo, isLogged);

    private readonly string GetDebuggerDisplay() {
        if (this.Exception is null) {
            return $"NoException";
        } else {
            return $"{this.Exception.GetType().Name} {this.Exception.Message}";
        }
    }

    public static OptionalErrorValue Uninitialized => OptionalErrorValueInstance.GetUninitialized();

    public static OptionalErrorValue CreateFromCatchedException(Exception exception) {
        var exceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception);
        return new OptionalErrorValue(exception, exceptionDispatchInfo, false);
    }

    public static Exception? GetAndSetIsLogged(ref OptionalErrorValue that) {
        that = that.WithIsLogged();
        return that.Exception;
    }

    public static implicit operator bool(OptionalErrorValue that)
        => (that.Exception is not null);

    public static implicit operator OptionalErrorValue(Exception exception)
        => new(exception, null, false);

    public static implicit operator OptionalErrorValue(ErrorValue error)
        => new(error.Exception, error.ExceptionDispatchInfo, error.IsLogged);

}

internal static class OptionalErrorValueInstance {
    internal static class Singleton {
        internal static OptionalErrorValue _Uninitialized = CreateUninitialized();
    }
    public static OptionalErrorValue GetUninitialized() => Singleton._Uninitialized;

    public static OptionalErrorValue CreateUninitialized() {
        var error = UninitializedException.Instance;
        var exceptionDispatchInfo = ExceptionDispatchInfo.Capture(error);
        return new OptionalErrorValue(error, exceptionDispatchInfo);
    }

}