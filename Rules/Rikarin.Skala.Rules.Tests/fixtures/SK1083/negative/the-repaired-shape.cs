public sealed class Registry {
    public static int Total(int[] numbers) {
        var total = 0;
        foreach (var number in numbers) {
            total += number;
        }

        return total;
    }
}
