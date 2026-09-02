// A comment inside the declaration the fix deletes is content, and a fix that silently deletes it is
// a fix nobody can review.
namespace Fixtures {
    sealed class Annotated {
        internal string Render(string text) {
            // Four is what the report template expects.
            return Render(text, 4);
        }

        internal string Render(string text, int indent) => new string(' ', indent) + text;
    }
}
