public sealed class Registry {
    public static int Reverse(int[] numbers) {
        var total = 0;
        for (var i = numbers.Length - 1; i >= 0; i--) {
            total += numbers[i];
        }

        return total;
    }
}
