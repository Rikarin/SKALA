// Deleting the overload stops the type implementing the interface — an error the fix would
// introduce rather than a review note.
namespace Fixtures {
    internal interface IRenderer {
        string Render(string text);
    }

    internal sealed class ContractRenderer : IRenderer {
        public string Render(string text) => Render(text, 4);

        public string Render(string text, int indent) => new string(' ', indent) + text;
    }
}
