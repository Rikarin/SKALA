// The names cross and the types do not agree, so the call would not compile the other way round
// and there is nothing to warn about. Same-typed neighbours are the whole hazard.
namespace Fixtures {
    sealed class Mixed {
        public void Run(string name, int count) {
            Write(count, name);
        }

        static void Write(int name, string count) { }
    }
}
