namespace Fixture {
    // A source type of the same name is never matched.
    public struct DateTime {
        public static bool TryParse(string text, out DateTime value) {
            value = default;
            return false;
        }
    }

    public sealed class Import {
        public bool Read(string text) => DateTime.TryParse(text, out _);
    }
}
