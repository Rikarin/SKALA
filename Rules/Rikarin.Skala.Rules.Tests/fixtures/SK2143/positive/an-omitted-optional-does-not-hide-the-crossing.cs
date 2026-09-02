// Fewer arguments than parameters is still positional: the omitted optionals come off the end, so
// every argument that was written still sits at its own parameter's index and the crossing is as
// visible as it would be in an exactly arity-matched call.
//
// ⚠ This fixture exists because the arity guard was first written as `!=`, which declined this and
// lost a true positive for no safety — the `params` filter is what makes over-supply unreachable,
// not the equality.
namespace Fixtures {
    sealed class Mover {
        public void Run(string source, string destination) {
            Copy(destination, source);
        }

        static void Copy(string source, string destination, int retries = 0) { }
    }
}
