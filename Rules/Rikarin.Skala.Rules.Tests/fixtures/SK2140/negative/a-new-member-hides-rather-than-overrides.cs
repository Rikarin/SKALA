// `new` hides; it does not override. There is no single declaration this one answers for, and
// the divergence is what the author wrote the keyword to ask for.
namespace Fixtures {
    class Writer {
        public virtual void Write(string text, bool flush = false) { }
    }

    sealed class ShadowWriter : Writer {
        public new void Write(string text, bool flush = true) { }
    }
}
