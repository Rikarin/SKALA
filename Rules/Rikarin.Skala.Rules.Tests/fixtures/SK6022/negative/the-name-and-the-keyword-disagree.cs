public sealed class PointStruct {
    public int X { get; init; }
}

public readonly struct OrderClass {
    public OrderClass(int id) {
        Id = id;
    }

    public int Id { get; }
}
