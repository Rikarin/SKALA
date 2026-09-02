// ⚠ Where the analysis stops, stated rather than hidden. The local function writes the captured
// parameter and the outer body reads it afterwards, so the incoming value really is discarded —
// but Roslyn's data flow over a body holding a local function is not ordered the way its reader
// is, and a verdict built on that ordering would be right here and wrong one edit away. Anything
// in the `Captured` set is left alone.
namespace Fixtures {
    sealed class Captured {
        public void Run(int seed) {
            void Assign() => seed = 7;

            Assign();
            System.Console.WriteLine(seed);
        }
    }
}
