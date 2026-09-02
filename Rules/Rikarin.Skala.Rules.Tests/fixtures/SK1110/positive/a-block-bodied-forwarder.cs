// The `return` form of the same shape, and a `this.` qualifier on the call.
namespace Fixtures {
    sealed class Reporter {
        string Format(string message) {
            return this.Format(message, true);
        }

        string Format(string message, bool loud) => loud ? message.ToUpperInvariant() : message;

        internal string Use(string message) => Format(message);
    }
}
