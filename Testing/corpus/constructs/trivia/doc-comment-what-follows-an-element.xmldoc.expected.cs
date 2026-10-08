// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaDocComments generated=2026-10-08
// #541 #542 #543 #544: what follows an element that spans lines or owns its line, an element whose
// content ends in an element, and the start tag's carry beside a break the author wrote.

class WhatFollowsAnElement {
    /// <remarks>
    ///     Lead
    ///     <i>
    ///         an italic run
    ///         over two lines
    ///     </i>
    ///     . A plan says what will happen.
    /// </remarks>
    void GluedFullStopAfterAnOpenedElement() { }

    /// <remarks>
    ///     Lead
    ///     <i>
    ///         an italic run
    ///         over two lines
    ///     </i>
    ///     and more prose.
    /// </remarks>
    void ProseAfterAnOpenedElement() { }

    /// <remarks>
    ///     Lead
    ///     <i>
    ///         an italic run
    ///         over two lines
    ///     </i>
    ///     , which continues.
    /// </remarks>
    void GluedCommaAfterAnOpenedElement() { }

    /// <remarks>
    ///     <para>
    ///         <b>
    ///             [docs/plan/31 D10]: a foliage type declares a collision shape and an activation
    ///             radius, and instances within that radius of a physics-relevant entity get a body.
    ///         </b>
    ///         Ten
    ///         thousand static bodies is not a scene, it is a broadphase problem.
    ///     </para>
    /// </remarks>
    void ProseAfterABoldRunTheAuthorBroke() { }

    /// <summary>Doc.</summary>
    /// <seealso cref="System.String" />
    /// .
    void GluedFullStopAfterATopLevelElement() { }

    /// <summary>Doc.</summary>
    /// <returns>Value.</returns>
    /// .
    int GluedFullStopAfterReturns() => 0;

    /// <remarks>
    ///     <list type="bullet">
    ///         <item>One.</item>
    ///     </list>
    ///     trailing.
    /// </remarks>
    void GluedWordAfterAList() { }

    /// <remarks>Lead <see cref="System.String" />, then words.</remarks>
    void GluedCommaAfterAnInlineElementStays() { }

    /// <summary>Doc <c>x</c>s and more.</summary>
    void GluedWordAfterCodeStays() { }

    /// <exception cref="System.ArgumentException">
    ///     When any element of <paramref name="destructuringPolicies" /> is
    ///     <code>null</code>
    /// </exception>
    void EndingInCodeOpens() { }

    /// <exception cref="System.ArgumentException">
    ///     When any element of <paramref name="destructuringPolicies" /> is <c>null</c>
    /// </exception>
    void EndingInCOpens() { }

    /// <exception cref="System.ArgumentNullException">
    ///     When <paramref name="destructuringPolicies" /> is <code>null</code>
    /// </exception>
    void EndingInCodeThatFitsWithItsEndTagStays() { }

    /// <exception cref="ArgumentException">When x x x x x x x x x x x x x x x x x x x x x x x x x x yy <c>null</c></exception>
    void FitsAtTheMarginCountingTheEndTag() { }

    /// <exception cref="ArgumentException">
    ///     When x x x x x x x x x x x x x x x x x x x x x x x x x x yyy <c>null</c>
    /// </exception>
    void OneColumnOverOpens() { }

    /// <remarks>
    ///     This type is currently internal, while we consider future directions for the logging pipeline, but should end up
    ///     public
    ///     in future.
    /// </remarks>
    void NoCarryBesideAnAuthorsBreak() { }

    /// <remarks>
    ///     This type is currently internal, while we consider future directions for the logging pipeline, but should end
    ///     up public in future, and then some more words to wrap again.
    /// </remarks>
    void CarryWithoutOne() { }

    /// <param name="averyveryverylongparametername">
    ///     This type is currently internal, while we consider future directions for the
    ///     logging pipeline.
    /// </param>
    void NoCarryInAParam(int averyveryverylongparametername) { }
}
