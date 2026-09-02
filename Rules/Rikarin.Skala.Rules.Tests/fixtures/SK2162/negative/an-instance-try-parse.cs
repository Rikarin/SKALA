namespace Fixture {
    // The method must be static on the framework type. An instance method of the same name on a
    // reader object is a different thing entirely.
    public sealed class Reader {
        public bool TryParse(string text, out int value) {
            value = 0;
            return false;
        }
    }

    public sealed class Import {
        public bool Read(Reader reader, string text) => reader.TryParse(text, out _);
    }
}
