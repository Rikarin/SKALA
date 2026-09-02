public static class Annotated {
    public static bool IsText(object value) => value is string /* deliberately unnamed */ _;
}
