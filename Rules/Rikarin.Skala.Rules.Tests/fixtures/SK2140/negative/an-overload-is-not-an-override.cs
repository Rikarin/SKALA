// Two overloads may disagree about their defaults as much as they like: neither is bound to the
// other, and overload resolution — not a static receiver type — decides which one a call reaches.
namespace Fixtures {
    sealed class Writer {
        public void Write(string text, bool flush = false) { }

        public void Write(string text, int repeat, bool flush = true) { }
    }
}
