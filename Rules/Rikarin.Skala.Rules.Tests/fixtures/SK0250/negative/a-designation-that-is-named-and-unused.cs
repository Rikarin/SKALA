public static class Ignoring {
    // ⚠ A name nothing uses is still a name. Whether it should be `_` is `SK6040`'s question about
    // an unused binding, not a redundancy in the pattern — and the two rules must not both offer
    // an edit for this span.
    public static bool IsText(object value) => value is string unused;
}
