public sealed class Mixed {
    static int stored;

    // The statement after the declaration assigns something else, so there is nothing adjacent to
    // join. The local's own assignment is a statement further down.
    public static int Count(int seed) {
        int count;
        stored = seed;
        count = seed + 1;
        return count;
    }
}
