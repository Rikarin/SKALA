class Base {
    public int Rank() => 1;
}

sealed class Leaf : Base {
    public int Read(int Rank) => Rank + base.Rank();
}
