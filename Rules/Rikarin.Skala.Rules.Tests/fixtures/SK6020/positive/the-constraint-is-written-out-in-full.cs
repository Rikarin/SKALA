public static class Qualified {
    public static string Text<T>(T value) where T : System.Enum => value.ToString()!;
}
