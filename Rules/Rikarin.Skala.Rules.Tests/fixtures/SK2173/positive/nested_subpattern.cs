class C {
    bool M(Box? box) => box is { Inner: not { } };
}

sealed class Box {
    public object? Inner { get; init; }
}
