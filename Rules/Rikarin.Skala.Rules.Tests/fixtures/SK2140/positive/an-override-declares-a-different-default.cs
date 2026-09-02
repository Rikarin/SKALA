// The default is baked into the call site from the static type, so a caller holding a `Base`
// flushes and a caller holding a `Derived` does not, from the same written call.
namespace Fixtures {
    abstract class Writer {
        public virtual void Write(string text, bool flush = false) { }
    }

    sealed class BufferedWriter : Writer {
        public override void Write(string text, bool flush = true) { }
    }
}
