// The author's own naming says which way round they meant it and the call says the other. Two
// strings, adjacent, so nothing in the type system can object.
namespace Fixtures {
    sealed class Mover {
        public void Run(string source, string destination) {
            Copy(destination, source);
        }

        static void Copy(string source, string destination) { }
    }
}
