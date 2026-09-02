namespace Fixtures {
    sealed class Runner {
        public Runner(int seed) => Seed = seed;

        public int Seed { get; }

        public static int Clamp(int value) {
            if (value < 0) {
                value = 0;
            }

            return value;
        }
    }
}
