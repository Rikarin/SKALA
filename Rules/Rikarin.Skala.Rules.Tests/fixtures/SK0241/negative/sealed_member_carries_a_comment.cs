class Base {
    public virtual void Flush() { }
}

sealed class Writer : Base {
    public sealed /* the base is still virtual for the other subclass */ override void Flush() { }
}
