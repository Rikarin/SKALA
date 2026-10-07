// ⚠ #425: `foreach` asks a struct for its enumerator through the property's copy and through the field's
// storage alike, but a mutable struct enumerable that counts its own enumerations sees the difference.
public struct Source {
    public int Enumerations;

    public System.Collections.Generic.IEnumerator<int> GetEnumerator() {
        Enumerations++;
        return new System.Collections.Generic.List<int> { 1 }.GetEnumerator();
    }
}

public sealed class Holder {
    private Source Items { get; set; }

    public Holder() => Items = new Source();

    public int Walk() {
        foreach (var _ in Items) {
        }

        return Items.Enumerations;
    }
}

public static class Probe {
    public static int Run() => new Holder().Walk();
}
