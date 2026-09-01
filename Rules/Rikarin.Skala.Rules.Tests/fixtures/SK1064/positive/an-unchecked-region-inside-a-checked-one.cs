// The nearest overflow context wins, so an `unchecked` inside a `checked` restores the rule.
public sealed class Nested {
    public int High(int hash) {
        checked {
            return unchecked((int)((uint)hash >> 16));
        }
    }
}
