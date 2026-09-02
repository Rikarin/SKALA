// `protected` on a public type is reachable from outside the assembly by deriving from it.
namespace Fixtures {
    public class ProtectedRenderer {
        protected string Render(string text) => Render(text, 4);

        protected string Render(string text, int indent) => new string(' ', indent) + text;
    }
}
