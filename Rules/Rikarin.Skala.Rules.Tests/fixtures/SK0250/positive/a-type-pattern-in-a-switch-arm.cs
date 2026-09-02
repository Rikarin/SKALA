public static class Sizing {
    public static int Rank(object value) =>
        value switch {
            int _ => 1,
            string _ => 2,
            _ => 0
        };
}
