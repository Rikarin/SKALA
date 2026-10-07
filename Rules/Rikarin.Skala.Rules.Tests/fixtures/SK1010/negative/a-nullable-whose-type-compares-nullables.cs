// ⚠ #425: `operator ==(Amount?, Amount?)` is not lifted, so `x == null` calls it with a null argument
// and `x is null` never does. This operator reads null as zero, and the two answer differently: measured
// for #412's audit as `True` before the fix and `False` after it.
public struct Amount {
    public int Value;

    public static bool operator ==(Amount? left, Amount? right) =>
        (left?.Value ?? 0) == (right?.Value ?? 0);

    public static bool operator !=(Amount? left, Amount? right) => !(left == right);

    public override bool Equals(object? other) => other is Amount amount && amount.Value == Value;

    public override int GetHashCode() => Value;
}

public static class Probe {
    public static bool Run() {
        Amount? zero = new Amount { Value = 0 };
        return zero == null;
    }
}
