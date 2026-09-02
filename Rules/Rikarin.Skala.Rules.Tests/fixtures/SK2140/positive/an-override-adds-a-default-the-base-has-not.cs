// The default reads as an offer that only holds through the derived type. A caller holding a
// `Reader` still has to pass the argument, and nothing in this declaration says so.
namespace Fixtures {
    abstract class Reader {
        public virtual void Read(string path, int limit) { }
    }

    sealed class LimitedReader : Reader {
        public override void Read(string path, int limit = 100) { }
    }
}
