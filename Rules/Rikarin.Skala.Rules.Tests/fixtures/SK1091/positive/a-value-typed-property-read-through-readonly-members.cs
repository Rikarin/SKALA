// A value-typed property is a field wherever nothing runs on it but `readonly` members: an `int`'s
// `ToString` and a `DateTime`'s `Year` read a copy and the storage alike. Pinned by Probe (#425).
public sealed class Holder {
    private int Total { get; set; }

    private System.DateTime Stamp { get; set; }

    public string Describe() {
        Total = 41;
        Total++;
        Stamp = new System.DateTime(2026, 10, 7);
        return Total.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/" + Stamp.Year;
    }
}

public static class Probe {
    public static string Run() => new Holder().Describe();
}
