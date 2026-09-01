class Base {
    public virtual void Flush() { }

    public virtual void Close() { }
}

class Writer : Base {
    public override void Flush() => base.Close();
}
