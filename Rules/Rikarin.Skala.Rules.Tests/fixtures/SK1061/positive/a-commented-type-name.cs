// ⚠ #302's shape (#325), on this rule's `typeof(T).Name` branch. The guard asked over the member
// access's FULL span, which begins after the `=>`, so the comment declined the finding. The fix
// replaces `typeof(Widget).Name` with `nameof(Widget)` and reaches nothing above it.
public sealed class Widget;

public sealed class Naming {
    public string TypeName() =>
        // the compiler already knows this; spelling it at run time costs a reflection call
        typeof(Widget).Name;
}
