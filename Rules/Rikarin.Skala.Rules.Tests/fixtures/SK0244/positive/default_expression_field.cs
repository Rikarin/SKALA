sealed class Store {
    long total = default;

    public void Add(long amount) => total += amount;

    public long Total => total;
}
