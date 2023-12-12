namespace Brimborium.ReturnValue;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
[global::Orleans.GenerateSerializer(GenerateFieldIds = Orleans.GenerateFieldIds.None, IncludePrimaryConstructorParameters = false)]
[global::Orleans.Immutable()]
public record struct ErrorValue(
    [property:global::Orleans.Id(0)]
    Exception Exception,
    ExceptionDispatchInfo? ExceptionDispatchInfo = default,
    [property:global::Orleans.Id(1)]
    bool IsLogged = false) {


    [DoesNotReturn]
    public readonly void Throw() {
        if (this.ExceptionDispatchInfo is not null) {
            this.ExceptionDispatchInfo.Throw();
        }

        if (this.Exception is not null) {
            throw this.Exception;
        }

        // TODO: better error
        throw new UninitializedException();
    }

    private readonly string GetDebuggerDisplay() {
        if (this.Exception is not null) {
            return $"{this.Exception.GetType().Name} {this.Exception.Message}";
        }
        return this.ToString();
    }

    public readonly ErrorValue WithIsLogged(bool isLogged = true)
        => new ErrorValue(this.Exception, this.ExceptionDispatchInfo, isLogged);

    public static ErrorValue Uninitialized => ErrorValueInstance.GetUninitialized();

    public static ErrorValue CreateFromCatchedException(Exception exception) {
        var exceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception);
        return new ErrorValue(exception, exceptionDispatchInfo);
    }
    
    public static Exception GetAndSetIsLogged(ref ErrorValue that) {
        that = that.WithIsLogged();
        return that.Exception;
    }

    public static implicit operator ErrorValue(Exception error) {
        return new ErrorValue(error, null, false);
    }
}

internal static class ErrorValueInstance {
    internal static class Singleton {
        internal static ErrorValue _Uninitialized = CreateUninitialized();
    }
    public static ErrorValue GetUninitialized() => Singleton._Uninitialized;

    public static ErrorValue CreateUninitialized() {
        var error = UninitializedException.Instance;
        var exceptionDispatchInfo = ExceptionDispatchInfo.Capture(error);
        return new ErrorValue(error, exceptionDispatchInfo);
    }

}
