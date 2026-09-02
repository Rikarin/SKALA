using System;

// ⚠ The same defect, and deliberately not reported: repairing it means changing a declaration every
// other member can read, which is not an edit to one file's text. A stated gap, not a judgement that
// the shape is fine.
public sealed class Work {
    readonly DateTime start = DateTime.UtcNow;

    public TimeSpan Elapsed() => DateTime.UtcNow - start;
}
