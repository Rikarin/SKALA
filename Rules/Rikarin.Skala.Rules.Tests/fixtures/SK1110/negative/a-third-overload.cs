// Deleting one overload changes which candidate wins for every call that used it, and with a third
// in the set the new winner need not be the one the body forwarded to.
namespace Fixtures {
    sealed class ThreeWay {
        internal string Render(string text) => Render(text, 4);

        internal string Render(string text, int indent) => new string(' ', indent) + text;

        internal string Render(object value) => value.ToString() ?? string.Empty;
    }
}
