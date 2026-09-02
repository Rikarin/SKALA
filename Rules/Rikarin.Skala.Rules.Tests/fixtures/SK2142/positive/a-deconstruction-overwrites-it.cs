// The write is a deconstruction target rather than an assignment's left-hand side. Data flow
// answers the question either way; only the reported location has to find the identifier.
namespace Fixtures {
    sealed class Splitter {
        public void Split(int first) {
            (first, var second) = (1, 2);
            System.Console.WriteLine(first + second);
        }
    }
}
