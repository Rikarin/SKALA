// The root of a hierarchy answers for nothing above it. A default here is the definition of the
// contract rather than a divergence from one.
namespace Fixtures {
    abstract class Reader {
        public abstract void Read(string path, int limit = 100);
    }
}
