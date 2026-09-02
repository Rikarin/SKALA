public sealed class Doubling {
    static int Compute(int order) => order * 2;

    static void Report(int value) { }

    public static int Total(int order) {
        var result = Compute(order);
        Report(result);
        return result;
    }
}
