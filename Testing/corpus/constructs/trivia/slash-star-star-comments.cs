// Issue #415, SK-DIV-0180. Roslyn lexes `/**` as a documentation comment wherever it stands, an
// argument list included, and the trivia's Span leaves the `/**` out; every file holding one was
// refused with SK9099. The corpus held none, which is how it went unseen. The oracle treats each of
// these as the block comment it looks like.
class C {
    /** <summary>On one line.</summary> */
    public void M(int x, int y) { }

    /**
     * <summary>Starred.</summary>
     */
    public void T() {
        /** before a statement */
        M(1, /** e10 */ 2);
        var a = new[] { 1, /** g */ 2 };
        var b = 1 /** h */ + 2;
        var z = 1 + /**/ 2;
        var w = 1 + /***/ 2;
        M(1, 2); /** trailing */
    }

    /** single */ public int F;

    public void E() {
        /** s1 */ E();
        Compute(alphaArgumentValue, /** f */ betaArgumentValue, gammaArgumentValue, /** g */ deltaArgumentValue, epsilonArgumentValue);
    }
}
