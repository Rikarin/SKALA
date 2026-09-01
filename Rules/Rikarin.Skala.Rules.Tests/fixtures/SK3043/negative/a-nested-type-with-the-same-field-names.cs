public sealed class Host {
    readonly object first = new();

    readonly object second = new();

    int value;

    public void Outer() {
        lock (first) {
            lock (second) {
                value++;
            }
        }
    }

    /// <summary>
    ///     ⚠ This fixture pins a Roslyn behaviour, not a branch of the analyzer. A nested type's
    ///     declarations sit inside the outer type's, and the pairs are keyed on the field name —
    ///     these fields carry the same names as `Host`'s — so if a symbol-start syntax action saw
    ///     nested declarations, `Host` would inherit this hierarchy and report an order neither type
    ///     has. It does not: Roslyn scopes the action to the symbol's own members. The analyzer
    ///     carried a guard for this and deleting it left this fixture green, which is what proved the
    ///     guard dead. The fixture stays so that the day Roslyn changes, this goes red.
    /// </summary>
    sealed class Inner {
        readonly object first = new();

        readonly object second = new();

        int value;

        public void Reversed() {
            lock (second) {
                lock (first) {
                    value--;
                }
            }
        }
    }
}
