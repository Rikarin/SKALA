// Anti-vacuity for every positive above: the ordinary member, where the parameter is read and
// never written. A rule keyed on "the parameter is not in DataFlowsIn" alone would still be
// silent here, but one keyed on the wrong half of the pair would not.
namespace Fixtures {
    sealed class Plain {
        public void Render(string path) {
            System.Console.WriteLine(path);
        }

        public string Expression(string path) => path;
    }
}
