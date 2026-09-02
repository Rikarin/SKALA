public static class WithAnInitializer {
    // The creation's span runs past the closing paren when an initializer is written, so the tail
    // deletion would carry the braces away with it. Refused on the shape rather than on the span.
    public static int? Go(int value) {
        int? wrapped = new int?(value) { };
        return wrapped;
    }
}
