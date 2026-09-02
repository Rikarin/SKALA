// ⚠ #302's shape (#325), and one of the few sites where it takes a real `///` on a real
// declaration. A field with no modifiers begins at its type, so the doc comment is leading trivia
// of the `System.Nullable<double>` node the guard was asked about — while the fix replaces that
// type name with `double?` and cannot reach the line above it.
public sealed class Reading {
    /// <summary>The last reading taken, if one has been taken at all.</summary>
    System.Nullable<double> temperature = 21.5;

    public double Value => temperature ?? 0;
}
