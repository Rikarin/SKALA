// `618` and `CS0618` are the same id, so the restore closes the disable and the region between
// them is what the rule measures.
public sealed class Work {
    public void Run() { }
}

#pragma warning disable 618 // The obsolete call this bracketed is gone.
#pragma warning restore CS0618
