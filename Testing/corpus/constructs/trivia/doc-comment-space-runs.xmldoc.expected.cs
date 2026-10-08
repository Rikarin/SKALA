// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaDocComments generated=2026-10-08
// #569: the author's run of spaces between words and around an element survives wherever the line does not
// break there, and counts toward the line's width.

class SpaceRuns {
    /// <summary>alpha  beta</summary>
    void M0() { } // double, flat, fits

    /// <summary>
    ///     alpha  beta
    /// </summary>
    void M1() { } // double, opened, fits

    /// <summary>End.  Next sentence.</summary>
    void M2() { } // after a period

    /// <summary>alpha   beta</summary>
    void M3() { } // triple

    /// <summary>
    ///     word00 word01 word02 word03 word04 word05 word06 word07 word08 word09 word10 word11 word12 word13 objects  is equal
    ///     word00 word01 word02 word03 word04 word05 word06 word07 word08 word09 word10 word11 word12 word13
    /// </summary>
    void M4() { } // double in a paragraph that wraps

    /// <summary>
    ///     xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx aaaaaaaaa
    ///     bbbbbbbbbbbbbbbbbbbbbb
    /// </summary>
    void M5() { } // double at the wrap point

    /// <returns>
    ///     <see langword="true" /> if the specified object  is equal to the current object; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    void M6() { } // the serilog line

    /// <summary>alpha  <c>x</c>  beta</summary>
    void M7() { } // double around an element

    /// <summary>
    ///     alpha  beta
    ///     gamma  delta
    /// </summary>
    void M8() { } // double on two kept lines
}
