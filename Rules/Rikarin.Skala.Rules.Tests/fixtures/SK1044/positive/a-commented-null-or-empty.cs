// ⚠ #302's shape (#325). The guard asked over the `||` expression's FULL span, so a comment placed
// inside the `if (` — the one place a reader would explain a legacy null check — declined the
// finding. The fix replaces the whole comparison with `string.IsNullOrEmpty(name)` and rewrites
// nothing above it.
public sealed class Naming {
    public static string Display(string? name) {
        if (
            // legacy callers are still allowed to pass null here
            name == null || name.Length == 0) {
            return "anonymous";
        }

        return name.ToUpperInvariant();
    }
}
