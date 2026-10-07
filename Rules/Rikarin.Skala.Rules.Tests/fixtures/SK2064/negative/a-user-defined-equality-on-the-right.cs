// ⚠ #423: `left == right` on a type with its own `operator ==` is a call, and so the right operand
// is not free to skip.
public sealed class Key {
    public static int Comparisons;

    public static bool operator ==(Key? a, Key? b) {
        Comparisons++;
        return ReferenceEquals(a, b);
    }

    public static bool operator !=(Key? a, Key? b) => !(a == b);

    public override bool Equals(object? obj) => obj is Key other && this == other;

    public override int GetHashCode() => 0;
}

public static class Probe {
    public static int Run() {
        var a = Key.Comparisons < 0;
        var key = new Key();
        var both = a & key == null;
        return both ? -1 : Key.Comparisons;
    }
}
