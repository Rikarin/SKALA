class Base {
    public virtual void Flush() { }
}

sealed class Writer : Base {
    public sealed override void Flush() { }
}
