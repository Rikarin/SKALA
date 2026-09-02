public sealed class Pricing {
    static int Compute(int order) => order * 2;

    public static int Total(int order) {
        var result = Compute(order);
        return result;
    }
}
