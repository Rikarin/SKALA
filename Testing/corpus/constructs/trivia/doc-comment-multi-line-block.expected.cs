// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// #568, SK-DIV-0380: which multi-line /** … */ blocks the oracle rebuilds as starred blocks, and which it
// leaves as written. Two shapes it rebuilds keeping the stars as text are not here: Skala leaves them.

class MultiLineBlocks {
    /**
     * a
     * b
     */
    void M0() { } // A first line text, starred second

    /** text
     */
    void M1() { } // B text then closer

    /**
     * text
     */
    void M2() { } // C starred, closer glued

    /**
     * text
     */
    void M3() { } // D canonical

    /**
     *no space
     */
    void M4() { } // E no space after star

    /**
    <summary>unstarred</summary>
    */
    void M5() { } // F unstarred at col 4

    /**
     * <summary>Doc.</summary>
     * <remarks>Unstarred second.</remarks>
     */
    void M6() { } // G unstarred second, closer glued

    /** <summary>a</summary>
     */
    void M7() { } // H element then closer

    /**
     * a
     * b
     */
    void M8() { } // I text, starred second with closer

    /**
     * a
     * b
     */
    void M9() { } // J canonical two

    /**
      * a
     */
    void M10() { } // K misaligned star

    /**
     * <summary>blank first</summary>
     */
    void M11() { } // L blank first

    /**
     * a
     * b
     */
    void M12() { } // M unstarred aligned second

    /**
       a
     */
    void M13() { } // N no stars

    /**
     * a
     */
    void M14() { } // O one line

    /**
     * <summary>Doc.</summary>
     */
    void M15() { } // P canonical summary

    /**
     * <summary>
     *     Doc.
     * </summary>
     */
    void M16() { } // Q canonical opened summary, unindented

    /**
     * <summary>
     *     Doc.
     * </summary>
     */
    void M17() { } // R canonical opened summary, indented

    /**
     * a
     * b
     */
    void M18() { } // A1 second starred, no space

    /**
     * a
     * b
     */
    void M19() { } // A2 second misaligned star

    /**
     * a
     * b
     */
    void M20() { } // A3 second unstarred at col 4, closer at col 4

    /**
     * a
     * b
     */
    void M21() { } // A4 second deep

    /**
     * a
     * b
     */
    void M22() { } // A5 closer at col 4

    /**
     * a
    */
    void M23() { } // D1 empty opener, closer at col 4

    /**
     * a
       */
    void M24() { } // D2 closer deep

    /**
     * a
     * b
     * c
     */
    void M25() { } // A6 three lines

    /**
     * a
     * b
     */
    void M26() { } // D4 second with two spaces after star

    /**
     * a
     */
    void M27() { } // B1 opener text, empty line, closer
}
