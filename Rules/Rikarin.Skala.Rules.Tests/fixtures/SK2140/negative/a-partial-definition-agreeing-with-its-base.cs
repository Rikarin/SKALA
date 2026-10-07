// #400: a partial method's defaults are written on its definition, and its implementation omits them
// (writing one there is CS1066 and changes nothing). The definition agrees with the base, so the
// method does.
namespace Fixtures {
    abstract class Writer {
        public virtual void Write(string text, bool flush = false) { }
    }

    sealed partial class BufferedWriter : Writer {
        public override partial void Write(string text, bool flush = false);

        public override partial void Write(string text, bool flush) { }
    }
}
