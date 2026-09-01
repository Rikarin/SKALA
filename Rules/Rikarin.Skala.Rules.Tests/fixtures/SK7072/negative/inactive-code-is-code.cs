// ⚠ Text inside a false `#if` is code under another set of preprocessor symbols. The rule counts
// it as content and stays quiet, because it cannot see the build that switches it on.
public sealed class Work {
    public void Run() { }
}

#pragma warning disable CS0168 // Guards the legacy path below.
#if LEGACY
        int unused;
#endif
#pragma warning restore CS0168
