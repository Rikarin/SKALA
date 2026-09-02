// ⚠ A field that is never assigned is CS0649, and `fixIsSafe: true` promises the fix does not
// break a TreatWarningsAsErrors build.
public sealed class ReadOnly {
    private int Total { get; }

    public int Value() => Total;
}
