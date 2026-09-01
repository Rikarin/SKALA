class Base {
    public Base(int capacity) => Capacity = capacity;

    public int Capacity { get; }
}

class Store : Base {
    public Store(int capacity) : base(capacity) { }
}
