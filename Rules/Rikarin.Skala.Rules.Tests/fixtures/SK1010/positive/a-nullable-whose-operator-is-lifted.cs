// `operator ==(Amount, Amount)` is lifted over `Amount?`, and a lifted operator answers a null operand
// itself without calling the user's: `x == null` is `!x.HasValue` exactly as `x is null` is. Pinned by
// Probe (#425).
public struct Amount {
    public int Value;

    public static bool operator ==(Amount left, Amount right) => true;

    public static bool operator !=(Amount left, Amount right) => false;

    public override bool Equals(object? other) => other is Amount amount && amount.Value == Value;

    public override int GetHashCode() => Value;
}

public static class Probe {
    public static string Run() {
        Amount? some = new Amount { Value = 0 };
        Amount? none = null;
        return (some == null) + "/" + (none == null);
    }
}
