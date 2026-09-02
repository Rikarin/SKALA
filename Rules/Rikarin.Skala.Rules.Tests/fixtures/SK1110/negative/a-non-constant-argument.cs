// Only a compile-time constant can become a default. `text.Length` is a different method, not a
// defaulted one.
namespace Fixtures {
    sealed class Dynamic {
        internal string Render(string text) => Render(text, text.Length);

        internal string Render(string text, int indent) => new string(' ', indent) + text;
    }
}
