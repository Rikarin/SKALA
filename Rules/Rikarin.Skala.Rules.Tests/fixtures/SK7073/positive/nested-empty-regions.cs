// Both regions are reported in one pass: an outer region holding only an inner empty one is a
// single edit, not a fix that has to be run twice.
public sealed class Work {
    #region Outer
    #region Inner
    #endregion
    #endregion

    public void Run() { }
}
