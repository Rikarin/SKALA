// A qualified member access, a call and a literal carry no name of their own to cross with a
// parameter's. Widening past a bare identifier is where a rule like this starts reporting
// ordinary code — an expression's *spelling* is not its author's claim about what it holds.
namespace Fixtures {
    sealed class Indirect {
        string Source { get; } = string.Empty;

        string Destination { get; } = string.Empty;

        public void Run() {
            Copy(this.Destination, this.Source);
            Copy(Read("destination"), Read("source"));
            Copy("destination", "source");
        }

        static string Read(string key) => key;

        static void Copy(string source, string destination) { }
    }
}
