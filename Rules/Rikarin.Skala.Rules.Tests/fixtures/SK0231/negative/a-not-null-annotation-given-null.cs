// ⚠ #412's audit: `string` is a promise the caller broke with `null!`. The flow state reads NotNull,
// and the call is the only thing that turns the broken promise into an exception here.
#nullable enable
public static class Labels {
    public static string Describe(string name) => name.ToString();
}

public static class Probe {
    public static string Run() => Labels.Describe(null!) ?? "<null returned>";
}
