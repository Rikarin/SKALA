// ⚠ #424: `!(a == b) switch { … }` switches on the negation; `a != b switch { … }` switches on `b`
// and compares `a` with the result. `BindsTighterThanEquality` had no row for a `switch`
// expression, and #412's audit measured True becoming False. The fix is re-parsed in place now.
public static class Probe {
    static bool Pick(bool a, bool b) =>
        !(a == b) switch {
            true => a,
            false => b
        };

    public static bool Run() => Pick(true, true);
}
