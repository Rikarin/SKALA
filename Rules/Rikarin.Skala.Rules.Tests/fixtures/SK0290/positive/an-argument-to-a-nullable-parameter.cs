public static class ArgumentPosition {
    static bool Accept(int? value) => value.HasValue;

    public static bool Go(int value) => Accept(new int?(value));
}
