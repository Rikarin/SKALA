public sealed class Registry {
    // ⚠ This loop runs one element past the end, and that is the point of the fixture: `foreach` would
    // quietly *fix* it, turning a program that throws into one that does not. The rule reports loop
    // shapes, not bugs, so `<=` is refused rather than repaired.
    //
    // ⚠ It said `i <= numbers.Length - 1` when it was written, which is a correct loop — and a
    // sabotage that widened the operator to `<=` left every test green, because the computed bound was
    // being refused by the "the bound is the receiver's own count" guard one line further down. Two
    // guards, one fixture, and neither of them held.
    public static int Total(int[] numbers) {
        var total = 0;
        for (var i = 0; i <= numbers.Length; i++) {
            total += numbers[i];
        }

        return total;
    }
}
