// The lambda half of the `Captured` stop. The delegate is built here and invoked somewhere
// else entirely, so no ordering the analysis can see decides whether the caller's value was
// read — and a verdict either way would be a guess.
namespace Fixtures {
    sealed class Deferred {
        public System.Action Build(int seed) {
            System.Action assign = () => seed = 7;
            assign();
            return () => System.Console.WriteLine(seed);
        }
    }
}
