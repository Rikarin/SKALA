struct IndexerSetterFixture {
    readonly int[] values;

    public IndexerSetterFixture(int[] items) => values = items;

    public int this[int index] {
        get => values[index];
        set => values[index] = value;
    }
}
