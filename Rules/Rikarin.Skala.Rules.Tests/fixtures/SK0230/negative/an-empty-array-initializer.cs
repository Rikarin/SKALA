public static class Arrays {
    // `new int[] { }` cannot lose its braces and still compile, so the array creation node is
    // deliberately never matched.
    public static int[] Empty() => new int[] { };
}
