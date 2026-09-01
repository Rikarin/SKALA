class Store {
    readonly int capacity;

    public Store() : this(8) { }

    public Store(int capacity) => this.capacity = capacity;

    public int Capacity => capacity;
}
