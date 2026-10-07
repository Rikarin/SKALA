// ⚠ #424, the SK0250 shape in this rule: `case Point { }:` is a type test and `case Point:` is the
// constant `Point`, because a bare type in a pattern is bound as an expression first.
public sealed class Point { }

public static class Probe {
    const string Point = "p";

    static string Name(object o) {
        switch (o) {
            case Point { }:
                return "point";

            default:
                return "other";
        }
    }

    public static string Run() => Name("p") + " " + Name(new Point());
}
