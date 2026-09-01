class Base {
    protected int Seen;
}

class Store : Base {
    readonly int capacity;

    public Store(int capacity) : base() => this.capacity = capacity;

    public int Capacity => capacity;
}
