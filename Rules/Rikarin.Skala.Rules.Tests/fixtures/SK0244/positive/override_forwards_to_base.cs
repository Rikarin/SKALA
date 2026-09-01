class Base {
    public virtual void Flush(int count, string label) { }
}

class Writer : Base {
    public override void Flush(int count, string label) => base.Flush(count, label);
}
