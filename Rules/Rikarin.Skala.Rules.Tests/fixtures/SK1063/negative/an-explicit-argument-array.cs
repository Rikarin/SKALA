// ⚠ One argument, not two: `{0}` means `values[0]`, and the interpolated form would print the
// array itself.
public sealed class Spread {
    public string Line(object[] values) => string.Format("{0} {1}", values);
}
