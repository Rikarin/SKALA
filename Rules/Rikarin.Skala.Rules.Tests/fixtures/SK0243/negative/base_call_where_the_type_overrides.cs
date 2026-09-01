class Base {
    public virtual int Rank() => 1;
}

sealed class Leaf : Base {
    public override int Rank() => base.Rank() + 1;
}
