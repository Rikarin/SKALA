// ⚠ C# 14's null-conditional assignment, and it does *not* parse the way the page reads: the
// assignment is the conditional part rather than the parent of the conditional access. The
// conditional write is the whole point of the line, so a modification that *is* the conditional part
// is the one arrangement never reported. This fixture is what refuted the opposite claim.
public sealed class Box {
    public int Value { get; set; }
}

public sealed class Setter {
    public void Store(Box? box) {
        box?.Value = 1;
    }
}
