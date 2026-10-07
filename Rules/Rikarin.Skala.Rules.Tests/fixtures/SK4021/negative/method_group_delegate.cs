using System;

// ⚠ #412's audit: a delegate over an instance method carries its instance, so two widgets hand out two
// unequal handlers. Made static, both are the same cached delegate with no target, and an
// `event -= handler` would remove the other widget's subscription.
public sealed class Widget {
    public Func<int> Handler() => Compute;

    private int Compute() => 42;
}

public static class Probe {
    public static string Run() {
        var first = new Widget().Handler();
        var second = new Widget().Handler();
        return (first.Target is null) + " " + first.Equals(second) + " " + first();
    }
}
