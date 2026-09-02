namespace Fixtures {
    sealed class Renderer {
        internal string Render(string text) => Render(text, 4);

        internal string Render(string text, int indent) => new string(' ', indent) + text;

        internal string Use(string text) => Render(text);
    }
}
