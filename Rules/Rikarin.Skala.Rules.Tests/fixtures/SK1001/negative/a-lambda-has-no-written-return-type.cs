using System;

// A lambda's return type is inferred from its body, so there is nothing for `[…]` to take a target
// type from and `() => […]` does not compile.
public sealed class Names {
    public Func<string[]> All() {
        return () => new string[] { "a" };
    }
}
