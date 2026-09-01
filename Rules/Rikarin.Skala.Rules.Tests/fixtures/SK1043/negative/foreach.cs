public sealed class Iterating {
    static void Consume(int value) { }

    public static void Walk(int[] values) {
        foreach (var value in values) {
            Consume(value);
        }
    }
}
