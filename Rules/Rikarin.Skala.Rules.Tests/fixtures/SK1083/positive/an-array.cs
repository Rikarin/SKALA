public sealed class Registry {
    public static int Total(int[] numbers) {
        var total = 0;
        for (var i = 0; i < numbers.Length; i++) {
            total += numbers[i];
        }

        return total;
    }
}
