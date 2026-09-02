// A local function has its own parameter list and its own body, and the same question applies.
// ⚠ The parameter overwritten here is the *local function's own*, not a captured one — a captured
// parameter is where this analysis stops, and the negatives pin that.
namespace Fixtures {
    sealed class Nested {
        public void Run() {
            Inner(1);

            static void Inner(int seed) {
                seed = 7;
                System.Console.WriteLine(seed);
            }
        }
    }
}
