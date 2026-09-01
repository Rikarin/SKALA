public sealed class Object { }

public static class Extensions {
    public static string Dump(this Object value) => value.ToString()!;
}
