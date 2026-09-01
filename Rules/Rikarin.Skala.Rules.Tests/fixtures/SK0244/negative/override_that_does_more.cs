class Base {
    public virtual void Flush() { }
}

class Writer : Base {
    public override void Flush() {
        base.Flush();
        Record();
    }

    static void Record() { }
}
