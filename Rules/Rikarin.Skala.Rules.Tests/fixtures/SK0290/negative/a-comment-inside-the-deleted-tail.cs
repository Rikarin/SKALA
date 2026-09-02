public static class CommentInTheTail {
    // The second deleted span runs from the operand's end to the closing paren, and it is asked the
    // same question the first one is.
    public static int? Go(int value) {
        int? wrapped = new int?(value /* the wrapper is deliberate */);
        return wrapped;
    }
}
