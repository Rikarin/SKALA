// ⚠ `NaN > 5` is false, so `not (> 5)` matches NaN and `<= 5` does not. The rewrite looks like De
// Morgan's law and is a behaviour change.
public sealed class Gate {
    public bool Small(double value) => value is not (> 5);
}
