public sealed class Buckets {
    readonly int[] counts = new int[8];

    // The long form evaluates the indexer twice and the compound form evaluates it once.
    public void Bump(int index) {
        counts[index] = counts[index] + 1;
    }
}
