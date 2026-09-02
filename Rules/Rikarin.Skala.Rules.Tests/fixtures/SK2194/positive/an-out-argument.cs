namespace Fixtures {
    sealed class Parsed(int count) {
        public bool Read(string text) => int.TryParse(text, out count);

        public int Count => count;
    }
}
