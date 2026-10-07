// ⚠ #423: the null test and the length test read `Name` twice and `string.IsNullOrEmpty(s.Name)`
// reads it once. Measured for #412's audit with a getter that answers differently the second time:
// `NullReferenceException` before the fix, `False` after. A getter with a body is not storage.
public sealed class Source {
    public int Reads;

    public string? Name {
        get {
            Reads++;
            return Reads == 1 ? "x" : null;
        }
    }
}

public static class Probe {
    public static int Run() {
        var s = new Source();
        return s.Name == null || s.Name.Length == 0 ? -s.Reads : s.Reads;
    }
}
