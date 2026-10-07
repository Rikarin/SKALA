class Base {
    public virtual void Flush() { }
}

// #400: the documentation comment of a partial override is on its definition, and the forwarding
// body is on its implementation. The comment is the declaration's reason for existing either way.
partial class Writer : Base {
    /// <summary>Present so the generated docs list it on this type too.</summary>
    public override partial void Flush();

    public override partial void Flush() => base.Flush();
}
