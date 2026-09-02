// ⚠ #302's shape (#325), on this rule's `as`-compared-to-null branch. The guard asked over the
// comparison's FULL span, which begins after the `=>`, so the sentence explaining the throwaway
// conversion silenced the rule. The fix rewrites only `shape as Circle != null` into a pattern.
public abstract class Shape;

public sealed class Circle : Shape;

public sealed class Inspector {
    public bool IsCircle(Shape shape) =>
        // a conversion performed only so that it can be thrown away again
        shape as Circle != null;
}
