using System;

public static class Reference {
    public static string Text<T>(T value) where T : class, Enum => value.ToString()!;
}
