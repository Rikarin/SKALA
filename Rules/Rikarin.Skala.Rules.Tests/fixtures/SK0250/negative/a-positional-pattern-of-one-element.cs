// ⚠ #424: `(1) _` is a positional pattern — one element, through `ITuple` or `Deconstruct` — and
// `(1)` without its designation is the constant `1` in parentheses. Measured by #412's audit:
// `Probe.Run()` printed False as written and True after the old fix.
public static class Probe {
    public static bool Run() {
        object o = 1;
        return o is (1) _;
    }
}
