using System;

// A tuple element is transparent to the walk, not a reason to decline: with one candidate the tuple
// parameter target-types the element the same way for both spellings.
public static class Ordering {
    public static void Register((int Order, Func<int, bool> Predicate) rule) { }

    public static void Use() {
        Register((1, delegate(int x) { return x > 0; }));
    }
}
