public sealed class Registry {
    public static int EveryOther(int[] numbers) {
        var total = 0;
        for (var i = 0; i < numbers.Length; i += 2) {
            total += numbers[i];
        }

        return total;
    }
}
