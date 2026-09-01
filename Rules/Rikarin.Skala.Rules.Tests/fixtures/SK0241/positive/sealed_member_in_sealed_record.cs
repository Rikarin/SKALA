abstract record Node {
    public virtual int Rank => 0;
}

sealed record Leaf : Node {
    public sealed override int Rank => 1;
}
