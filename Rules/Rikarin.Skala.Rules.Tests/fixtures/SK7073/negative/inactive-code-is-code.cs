// ⚠ Text inside a false `#if` is code under another set of preprocessor symbols. Deleting the
// region around it would be deciding, from one build, that another build has nothing there.
public sealed class Work {
    #region Legacy
#if LEGACY
    public void Save() { }
#endif
    #endregion

    public void Run() { }
}
