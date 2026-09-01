using System;

public static class Unmanaged {
    public static string Text<T>(T value) where T : unmanaged, Enum => value.ToString();
}
