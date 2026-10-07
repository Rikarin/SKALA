// A concatenation is never null, so `ToString()` on it can throw nothing and returns the same string.
public static class Labels {
    public static string Describe(string name) => ("name: " + name).ToString();
}

public static class Probe {
    public static string Run() => Labels.Describe(null!);
}
