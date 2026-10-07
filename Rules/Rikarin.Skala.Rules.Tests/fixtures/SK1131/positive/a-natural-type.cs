using System;

// An anonymous method with explicit parameters has the same natural type as the lambda — measured
// at C# 10 and above, and both are CS8773 below it — so `var`, `Delegate` and `object` agree.
public static class Natural {
    public static object Make() {
        var increment = delegate(int x) { return x + 1; };
        Delegate untyped = delegate(int x) { return x - 1; };
        object boxed = delegate(int x) { return x * 3; };
        return (increment, untyped, boxed);
    }
}
