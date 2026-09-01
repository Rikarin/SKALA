sealed class Handle {
    public int Id { get; init; }
}

class C {
    bool Same(Handle left, Handle right) => left == right;
}
