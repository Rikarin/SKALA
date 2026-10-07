// ⚠ #424: `o is Kind` is the `is` operator and tests the type; `o is not Kind` is a pattern, and a
// pattern binds `Kind` as an expression first — the constant, the Color Color case. Measured by
// #412's audit: True/True became False/True.
public enum Kind {
    A,
    B
}

public sealed class Holder {
    const Kind Kind = Kind.A;

    public bool NotAKind(object o) => !(o is Kind);
}

public static class Probe {
    public static string Run() {
        var h = new Holder();
        return h.NotAKind(Kind.B) + " " + h.NotAKind(Kind.A);
    }
}
