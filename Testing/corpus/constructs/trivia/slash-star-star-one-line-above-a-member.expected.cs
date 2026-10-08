// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
/**
 * above the type
 */
class SlashStarStarOneLine {
    /**
     * <summary>Doc.</summary>
     */
    public int Rebuilt;

    /**
     * single
     */
    public int RebuiltWithoutAnElement;

    /**
     * single
     */
    public int RebuiltWithNoSpaceAfterTheOpener;

    /**
     * <summary>Doc.</summary>
     * <param name="a">A.</param>
     */
    public void RebuiltAndSplit(int a) { }

    /**
     * <summary>
     *     A very long block doc comment that will certainly not fit within the margin of one hundred and twenty columns
     *     at all.
     * </summary>
     */
    public int RebuiltAndWrapped;

    /**
     * <summary>Doc.</summary>
     */
    [System.Obsolete]
    public int RebuiltAboveAnAttribute;

    /** single*/
    public int LeftWithNoSpaceBeforeTheCloser;

    /**<summary>X</summary>*/
    public int LeftWithNoSpaceAtAll;

    [System.Obsolete] /** <summary>after an attribute</summary> */
    public int LeftAfterAnAttribute;

    void M() {
        /** above a statement */
        int x = 1;

        /** <summary>Doc.</summary> */
        void LeftAboveALocalFunction() { }
    }

    /// <summary>A <c>///</c> comment beside them, formatted as always.</summary>
    /// <remarks>Remarks.</remarks>
    public int Control;
}
