interface IHandle {
    int Id { get; }
}

class C {
    bool Same(IHandle left, IHandle right) => left == right;
}
