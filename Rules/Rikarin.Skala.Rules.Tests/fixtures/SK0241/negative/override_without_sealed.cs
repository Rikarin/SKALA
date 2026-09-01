class Base {
    public virtual void Flush() { }
}

sealed class Writer : Base {
    public override void Flush() { }
}
