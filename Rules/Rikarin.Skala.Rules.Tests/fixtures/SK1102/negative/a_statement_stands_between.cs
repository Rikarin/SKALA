public sealed class Interrupted {
    public static int Run(int seed) {
        int Work(int value) => value + 1;

        var total = Work(seed);
        return total * 2;
    }
}
