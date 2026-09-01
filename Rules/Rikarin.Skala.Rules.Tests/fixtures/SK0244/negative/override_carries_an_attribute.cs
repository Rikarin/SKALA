using System;

class Base {
    public virtual void Flush() { }
}

class Writer : Base {
    [Obsolete("use Close")]
    public override void Flush() => base.Flush();
}
