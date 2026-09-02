// Anti-vacuity: the same call written the right way round. A rule keyed on "the argument names
// match the parameter names" and not on the crossing would fire here.
namespace Fixtures {
    sealed class Mover {
        public void Run(string source, string destination) {
            Copy(source, destination);
        }

        static void Copy(string source, string destination) { }
    }
}
