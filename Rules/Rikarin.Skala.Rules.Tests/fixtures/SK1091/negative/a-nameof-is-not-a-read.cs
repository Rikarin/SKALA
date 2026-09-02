// ⚠ `nameof` keeps working over a field, so it cannot make the fix wrong — but the compiler
// does not credit it as a read either, and letting it stand in for one produces CS0414.
public sealed class Probe {
    private int Total { get; set; }

    public string Describe() {
        Total = 1;
        return nameof(Total);
    }
}
