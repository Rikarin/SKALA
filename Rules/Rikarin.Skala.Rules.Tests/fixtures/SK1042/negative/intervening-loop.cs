public sealed class Looping {
    static void Emit(int value) { }

    // A loop between the two conditions is not a nesting that can be flattened: the inner test
    // runs once per iteration.
    public static void Handle(bool a, int[] values) {
        if (a) {
            foreach (var value in values) {
                if (value > 0) {
                    Emit(value);
                }
            }
        }
    }
}
