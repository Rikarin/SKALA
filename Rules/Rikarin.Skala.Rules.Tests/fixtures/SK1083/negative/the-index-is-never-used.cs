public sealed class Registry {
    // A repeat count, not a walk: `foreach` would say something the original did not.
    public static int Count(int[] numbers) {
        var total = 0;
        for (var i = 0; i < numbers.Length; i++) {
            total++;
        }

        return total;
    }
}
