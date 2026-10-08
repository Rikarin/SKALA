// #451: every comment below is the oracle's own first-pass answer for the same comment written on one
// line. Given back, the oracle hoists the lead onto a line of its own, which is its fixed point and
// Skala's one-pass answer.

class LeadProseGivenBackIsHoisted {
    /// <remarks>Some leading prose.
    ///     <para>Short.</para>
    /// </remarks>
    void Kept() { }

    /// <summary>Some leading prose.
    ///     <para>Short.</para>
    /// </summary>
    void KeptInASummary() { }

    /// <param name="a">Lead.
    ///     <para>Short.</para>
    /// </param>
    void KeptInAParam(int a) { }

    /// <remarks>Some leading prose.
    ///     <para>Short.</para>
    ///     Trailing prose.
    /// </remarks>
    void KeptWithTrailingProse() { }

    /// <remarks>Some leading prose.
    ///     <para>Short.</para>
    ///     <para>Second.</para>
    /// </remarks>
    void KeptBesideTwoParagraphs() { }

    /// <remarks>Lead <see cref="System.String" /> more.
    ///     <para>Short.</para>
    /// </remarks>
    void KeptWithAnInlineElementInTheLead() { }

    /// <remarks>Lead.
    ///     <para />
    ///     Tail.
    /// </remarks>
    void KeptBesideAnEmptyParagraph() { }

    /// <remarks>Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor incididunt ut labore et.
    ///     <para>Short.</para>
    /// </remarks>
    void KeptWhileEveryLineFits() { }
}
