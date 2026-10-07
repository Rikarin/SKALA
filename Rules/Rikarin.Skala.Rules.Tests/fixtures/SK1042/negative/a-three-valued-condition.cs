// ⚠ #425: nested, each condition is asked `operator true` on its own; merged, `a && b` is
// `operator false(a) ? a : a & b`, and then `operator true` of the result. With a user `&` that is not
// "both true" the merged condition is another computation. Measured for #412's audit: "both" printed
// nested and not merged.
public readonly struct Tri {
    public readonly int Value;

    public Tri(int value) => Value = value;

    public static bool operator true(Tri tri) => tri.Value > 0;

    public static bool operator false(Tri tri) => tri.Value <= 0;

    public static Tri operator &(Tri left, Tri right) => new Tri(System.Math.Min(left.Value, right.Value) - 1);

    public static Tri operator |(Tri left, Tri right) => new Tri(System.Math.Max(left.Value, right.Value));
}

public static class Probe {
    public static string Run() {
        var a = new Tri(1);
        var b = new Tri(1);
        if (a) {
            if (b) {
                return "both";
            }
        }

        return "neither";
    }
}
