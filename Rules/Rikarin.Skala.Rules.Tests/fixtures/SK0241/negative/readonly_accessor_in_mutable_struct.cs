struct Counter {
    int count;

    public int Count {
        readonly get => count;
        set => count = value;
    }
}
