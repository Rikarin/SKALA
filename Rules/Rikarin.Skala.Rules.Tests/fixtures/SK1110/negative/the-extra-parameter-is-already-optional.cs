// There is nothing to collapse: the default is already where it belongs, and the pair compiles only
// because the shorter signature still wins for a one-argument call.
namespace Fixtures {
    sealed class AlreadyOptional {
        internal string Render(string text) => Render(text, 4);

        internal string Render(string text, int indent = 2) => new string(' ', indent) + text;
    }
}
