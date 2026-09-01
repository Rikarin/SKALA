class Base {
    public virtual void Flush() { }
}

class Writer : Base {
    /// <summary>Present so the generated docs list it on this type too.</summary>
    public override void Flush() => base.Flush();
}
