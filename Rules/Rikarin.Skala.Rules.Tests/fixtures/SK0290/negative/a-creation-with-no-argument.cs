public static class NoArgument {
    // There is no operand to leave behind: the deletion would produce nothing, not a shorter
    // expression.
    public static int? Go() {
        int? empty = new int?();
        return empty;
    }
}
