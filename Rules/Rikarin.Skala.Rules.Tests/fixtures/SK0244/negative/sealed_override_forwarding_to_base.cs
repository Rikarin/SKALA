class Base {
    public virtual void Flush() { }
}

class Writer : Base {
    public sealed override void Flush() => base.Flush();
}
