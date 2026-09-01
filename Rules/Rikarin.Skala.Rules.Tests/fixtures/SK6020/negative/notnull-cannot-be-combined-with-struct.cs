using System;

public static class NotNull {
    public static string Text<T>(T value) where T : notnull, Enum => value.ToString()!;
}
