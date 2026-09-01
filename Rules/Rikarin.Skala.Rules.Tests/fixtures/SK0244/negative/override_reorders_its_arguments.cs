class Base {
    public virtual void Flush(int count, int limit) { }
}

class Writer : Base {
    public override void Flush(int count, int limit) => base.Flush(limit, count);
}
