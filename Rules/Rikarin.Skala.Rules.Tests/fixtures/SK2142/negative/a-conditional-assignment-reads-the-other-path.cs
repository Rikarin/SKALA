// The commonest correct shape and the one a syntactic scan gets wrong: a default applied only
// when the caller did not supply one. The `else` path reads the incoming value, so it flows in
// and there is no finding. Nothing in this rule special-cases `if` — data flow answers it.
namespace Fixtures {
    sealed class Defaulting {
        public void Render(string path, bool useDefault) {
            if (useDefault) {
                path = "default.txt";
            }

            System.Console.WriteLine(path);
        }
    }
}
