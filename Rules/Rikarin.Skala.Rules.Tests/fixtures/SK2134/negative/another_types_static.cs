// ⚠ Only the enclosing type's own static fields. A process-wide setting on somebody else's type is
// usually set deliberately, once, by code that knows it — a different concept with a different
// answer — and admitting it would have made the exclusion list carry the rule.
static class Diagnostics {
    public static int Level;
}

sealed class Startup {
    public void Configure() {
        Diagnostics.Level = 3;
    }
}
