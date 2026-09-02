// Deleting an overload from a published surface is a binary break, and an optional parameter's
// default is compiled into every call site rather than read from the callee. This is the half of
// the ReSharper pair (`RedundantOverload.Global`) that issue #112 says must not ship.
namespace Fixtures {
    public sealed class PublicRenderer {
        public string Render(string text) => Render(text, 4);

        public string Render(string text, int indent) => new string(' ', indent) + text;
    }
}
