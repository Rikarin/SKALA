// C# admits no optional parameter before a required one, so there is no signature to collapse into.
namespace Fixtures {
    sealed class LeadingExtra {
        internal string Render(string text) => Render(4, text);

        internal string Render(int indent, string text) => new string(' ', indent) + text;
    }
}
