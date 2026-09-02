public sealed class Registry {
    // ⚠ The iteration variable is a copy; writing to it would silently stop updating the array.
    public static void Zero(int[] numbers) {
        for (var i = 0; i < numbers.Length; i++) {
            numbers[i] = 0;
        }
    }
}
