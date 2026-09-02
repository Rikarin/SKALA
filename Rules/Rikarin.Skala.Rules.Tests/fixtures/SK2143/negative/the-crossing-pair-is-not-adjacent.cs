// Non-adjacent names that happen to cross are not evidence of a transposition: with a parameter
// between them the call reads nothing like a slip of two neighbouring arguments, and reporting it
// would mean reporting every call whose arguments were named after some other parameter.
namespace Fixtures {
    sealed class Spread {
        public void Run(string source, string middle, string destination) {
            Copy(destination, middle, source);
        }

        static void Copy(string source, string middle, string destination) { }
    }
}
