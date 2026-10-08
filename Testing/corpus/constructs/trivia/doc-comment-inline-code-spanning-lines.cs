// #569: a <c> whose content spans lines is not verbatim — each line is trimmed and placed one indent past the
// tag, a blank one dropped, a long one wrapped at a space, the author's breaks and inner spaces kept.
class InlineCodeSpanningLines {
    /// <remarks>Lead <c>alpha
    ///     beta</c> trail.</remarks>
    void M0() { } // two lines, second indented

    /// <remarks>Lead <c>alpha
    /// beta
    ///         gamma</c> trail.</remarks>
    void M1() { } // three lines, relative indents

    /// <remarks>
    /// <c>
    /// alpha
    ///     beta
    /// </c>
    /// </remarks>
    void M2() { } // tags on their own lines

    /// <remarks>Lead <c>alpha  beta
    /// gamma   delta</c> trail.</remarks>
    void M3() { } // double spaces inside

    /// <remarks>Lead <c>word00 word01 word02 word03 word04 word05 word06 word07 word08 word09 word10 word11 word12 word13 word14 word15 word16 word17 word18 word19 word20 word21 word22 word23 word24 word25 word26 word27 word28 word29
    /// tail</c> trail.</remarks>
    void M4() { } // a long first line

    /// <summary>Lead <c>alpha
    /// beta</c></summary>
    void M5() { } // in a summary, ends the element

    /// <remarks>Lead <c>a  b</c> trail <c>one
    /// two</c>.</remarks>
    void M6() { } // flat with double space, then multi-line

    /// <remarks>
    /// <c>alpha
    ///
    /// beta</c>
    /// </remarks>
    void M7() { } // blank line inside
}
