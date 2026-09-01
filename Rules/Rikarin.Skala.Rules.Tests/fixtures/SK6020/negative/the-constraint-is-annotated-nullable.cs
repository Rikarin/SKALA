using System;

public static class Annotated {
    public static string Text<T>(T? value) where T : Enum? => value?.ToString() ?? string.Empty;
}
