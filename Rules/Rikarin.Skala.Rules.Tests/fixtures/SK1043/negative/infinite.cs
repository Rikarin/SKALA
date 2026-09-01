public sealed class Forever {
    static bool Done() => true;

    // `for (;;)` is the idiomatic infinite loop, not a `while` that lost its condition.
    // `while (true)` would say nothing the original does not.
    public static void Pump() {
        for (;;) {
            if (Done()) {
                return;
            }
        }
    }
}
