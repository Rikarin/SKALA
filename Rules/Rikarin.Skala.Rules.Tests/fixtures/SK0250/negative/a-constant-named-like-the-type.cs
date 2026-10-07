// ⚠ #424: `case Random _:` tests the type, but `case Random:` and `Random =>` are bound as an
// expression first, and the constant `Random` in scope takes them. Measured by #412's audit:
// "other type / other type" became "type other / type other".
public sealed class Random { }

public static class Probe {
    const string Random = "r";

    static string Name(object o) {
        switch (o) {
            case Random _:
                return "type";

            default:
                return "other";
        }
    }

    static string Arm(object o) =>
        o switch {
            Random _ => "type",
            _ => "other"
        };

    public static string Run() => Name("r") + " " + Name(new Random()) + " / " + Arm("r") + " " + Arm(new Random());
}
