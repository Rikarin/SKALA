class Base {
    public virtual int Rank() => 1;
}

class Middle : Base {
    public int Read() => base.Rank();
}

sealed class Leaf : Middle {
    public override int Rank() => 2;
}
