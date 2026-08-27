public static class Enum {
    public static object[] GetValues(System.Type type) => new object[0];
}

public sealed class Holder {
    public static object[] All() => Enum.GetValues(typeof(string));
}
