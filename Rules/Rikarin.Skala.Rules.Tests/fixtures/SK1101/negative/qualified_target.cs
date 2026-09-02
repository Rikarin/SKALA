public sealed class Qualified {
    int count;

    // The left side is not the bare identifier, so it does not name the local at all.
    public int Count(int seed) {
        int unused;
        this.count = seed;
        unused = seed;
        return unused;
    }
}
