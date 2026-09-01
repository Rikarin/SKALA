class Base {
    public int Rank() => 1;
}

class Leaf : Base {
    public int Read() => base.Rank();
}
