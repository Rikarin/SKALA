public sealed class Annotated {
    static int Compute(int order) => order * 2;

    public static int Total(int order) {
        var result = Compute(order);

        // The rounding here is the caller's problem and deliberately not done.
        return result;
    }
}
