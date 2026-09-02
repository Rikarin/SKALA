public sealed class Counter {
    readonly object gate = new();

    int value;

    public void Increment() {
        // ⚠ The keyword is out of scope by construction, not by a guard. `lock` lowers to exactly
        // `Monitor.Enter` plus `try`/`finally` `Monitor.Exit`, so it is always correct — and it
        // produces no invocation expression in the syntax tree for the rule to match. This fixture
        // exists because "cannot happen" is a claim, and an unasserted claim is worth nothing.
        lock (gate) {
            value = checked(value + 1);
        }
    }
}
