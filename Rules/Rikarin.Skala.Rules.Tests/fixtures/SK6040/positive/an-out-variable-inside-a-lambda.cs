using System;

public static class Filters {
    public static Func<string, bool> Numeric() => text => int.TryParse(text, out var number);
}
