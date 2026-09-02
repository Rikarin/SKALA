public sealed class Batched {
    public static int Run(int seed) {
        var total = First(seed) + Second(seed);

        int First(int value) => value + 1;

        int Second(int value) => value + 2;

        return total * 3;
    }
}
