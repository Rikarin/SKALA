static class Compare {
    // The qualifier is the declaring type, so there is nothing to move.
    public static bool Same(object a, object b) => object.ReferenceEquals(a, b);
}
