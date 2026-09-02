// ⚠ The shape that crashes SK0232 (issue #298). More arguments than parameters, so any walk
// indexing the parameter array by argument position reads off the end of it.
//
// The first call is the crosswise shape and a `params` call at once: `destination` and `source`
// really do cross the first two parameters, and the rule declines it because the arity does not
// match and "the parameter this argument fills" is therefore not a fact. The second is the bare
// crash reproduction.
namespace Fixtures {
    sealed class Expanded {
        public void Run(string source, string destination) {
            Copy(destination, source, 1, 2);
            Take(1, 2, 3);
        }

        static void Copy(string source, string destination, params int[] extra) { }

        static void Take(int first, params int[] rest) { }
    }
}
