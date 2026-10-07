class Base {
    public virtual void Flush(int count, string label) { }
}

// #400: a partial override that only forwards is as redundant as any other, and the fix deletes both
// halves — deleting the implementation alone leaves a definition with no body, which is CS8795.
partial class Writer : Base {
    public override partial void Flush(int count, string label);

    public override partial void Flush(int count, string label) => base.Flush(count, label);
}
