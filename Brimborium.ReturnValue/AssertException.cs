namespace Brimborium.ReturnValue;
public class AssertException : Exception {
    public static void Assert([DoesNotReturnIf(false)] bool condition, string message) {
        if (!condition) {
            throw new AssertException(message);
        }
    }
    public AssertException(string message) : base(message) { }
}
