using System;

public static class Dates {
    public static bool LooksLikeADate(string text) => DateTime.TryParse(text, out DateTime moment);
}
