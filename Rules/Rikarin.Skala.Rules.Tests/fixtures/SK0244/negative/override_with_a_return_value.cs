class Base {
    public virtual int Rank() => 1;
}

class Writer : Base {
    public override int Rank() => base.Rank();
}
