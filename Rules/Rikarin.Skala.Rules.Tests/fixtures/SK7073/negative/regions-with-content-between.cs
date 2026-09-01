// Two full regions in a row. Neither is empty, and the rule must not read the second region's
// opening directive as the first one's closing directive.
public sealed class Work {
    #region Reading
    public void Read() { }
    #endregion

    #region Writing
    public void Write() { }
    #endregion
}
