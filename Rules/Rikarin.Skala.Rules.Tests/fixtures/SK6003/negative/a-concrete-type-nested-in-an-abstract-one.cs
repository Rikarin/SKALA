public abstract class Importer {
    protected Importer() { }

    public sealed class Options {
        public Options(int depth) {
            Depth = depth;
        }

        public int Depth { get; }
    }
}
