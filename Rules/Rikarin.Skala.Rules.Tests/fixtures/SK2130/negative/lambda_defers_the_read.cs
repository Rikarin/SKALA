using System;

// ⚠ The body runs when somebody invokes the delegate, long after every initializer has finished.
// Deferring the read is the ordinary way to *repair* this defect, so firing here would report the
// repair rather than the bug.
static class Config {
    public static readonly Func<int> Value = () => Later;

    public static readonly int Later = 42;
}
