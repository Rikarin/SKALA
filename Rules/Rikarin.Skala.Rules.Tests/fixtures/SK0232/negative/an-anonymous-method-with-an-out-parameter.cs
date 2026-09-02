using System;

public delegate void TryIt(out int value);

public static class Failing {
    // ⚠ Nothing mentions `value`, and the signature still cannot go: a parameterless `delegate`
    // is convertible only to a delegate type with no `out` parameter, so the shortened form is
    // CS1688 rather than the same program. The body throws, which is the only way an `out`
    // parameter is legally never assigned — and therefore the only way this shape exists at all.
    public static TryIt Never = delegate(out int value) { throw new NotSupportedException(); };
}
