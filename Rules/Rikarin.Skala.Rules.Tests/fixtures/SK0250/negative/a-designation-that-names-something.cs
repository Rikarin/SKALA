public static class Reading {
    public static int Length(object value) => value is string text ? text.Length : 0;
}
