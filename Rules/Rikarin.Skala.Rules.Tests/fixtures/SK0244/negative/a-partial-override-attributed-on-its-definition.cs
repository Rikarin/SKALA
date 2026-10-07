using System;

class Base {
    public virtual void Flush() { }
}

// #400: an attribute on either half is an attribute on the override.
partial class Writer : Base {
    [Obsolete("Flush through the sink instead.")]
    public override partial void Flush();

    public override partial void Flush() => base.Flush();
}
