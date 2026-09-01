// ⚠ The same trap with a different value: `not (> 5)` matches `null` and `<= 5` does not.
public sealed class Gate {
    public bool Small(int? count) => count is not (> 5);
}
