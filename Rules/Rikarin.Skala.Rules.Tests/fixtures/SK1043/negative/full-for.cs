public sealed class Ordinary {
    static void Consume(int value) { }

    public static void Walk(int[] values) {
        for (var i = 0; i < values.Length; i++) {
            Consume(values[i]);
        }
    }
}
