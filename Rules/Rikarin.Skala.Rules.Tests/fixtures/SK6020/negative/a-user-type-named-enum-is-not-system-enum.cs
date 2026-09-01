public class Enum {
    public override string ToString() => "shadow";
}

public static class Shadowed {
    public static string Text<T>(T value) where T : Enum => value.ToString()!;
}
