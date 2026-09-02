public sealed class Registry {
    // `i <= numbers.Length - 1` is the same range written so a reader has to do the arithmetic. A rule
    // that guesses between that and `i <= numbers.Length` is a rule that rewrites the wrong loop.
    public static int Total(int[] numbers) {
        var total = 0;
        for (var i = 0; i <= numbers.Length - 1; i++) {
            total += numbers[i];
        }

        return total;
    }
}
