// The ordinary case: the override restates the base's default exactly, which is redundant text
// and not a defect — the call site gets the same value from either reference.
namespace Fixtures {
    abstract class Writer {
        public virtual void Write(string text, bool flush = false) { }
    }

    sealed class BufferedWriter : Writer {
        public override void Write(string text, bool flush = false) { }
    }
}
