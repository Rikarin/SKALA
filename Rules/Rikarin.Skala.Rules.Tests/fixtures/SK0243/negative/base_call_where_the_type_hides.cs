class Base {
    public int Rank() => 1;
}

sealed class Leaf : Base {
    public new int Rank() => base.Rank() + 1;
}
