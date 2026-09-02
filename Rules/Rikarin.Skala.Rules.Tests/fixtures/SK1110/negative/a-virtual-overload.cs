// The overload's existence may be a contract: a derived type can override it, and deleting it
// changes what that type is allowed to say.
namespace Fixtures {
    internal class Overridable {
        internal virtual string Render(string text) => Render(text, 4);

        internal virtual string Render(string text, int indent) => new string(' ', indent) + text;
    }
}
