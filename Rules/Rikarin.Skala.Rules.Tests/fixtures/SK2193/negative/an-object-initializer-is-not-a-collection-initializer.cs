namespace Fixtures {
    sealed class Options {
        public int Size { get; set; }

        public static Options Default() => new Options { Size = 4 };
    }
}
