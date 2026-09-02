// ⚠ #302's shape (#325). The guard asked over the `string.Format` invocation's FULL span, which
// begins after the `=>`, so the caption's own explanation declined the finding. The fix rewrites
// only the call into an interpolated string and leaves the comment above it untouched.
public sealed class Progress {
    public string Line(int done, int total) =>
        // the caption the user actually sees
        string.Format("{0} of {1}", done, total);
}
