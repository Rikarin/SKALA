// ⚠ #412's audit: in an oblivious context the flow state is not MaybeNull, and the parameter holds
// null. `name.ToString()` throws; `name` would hand the null on to the caller.
#nullable disable
public static class Labels {
    public static string Describe(string name) => name.ToString();
}

public static class Probe {
    public static string Run() => Labels.Describe(null) ?? "<null returned>";
}
