// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// Issue #382: the oracle keeps a doc comment that is, as a whole, what it would write at one of the
// key's two values, and rebuilds it otherwise. The sibling file one directory up crams two elements
// onto one line, so it is rebuilt at either value and cannot tell a per-line rule from a per-comment
// one. Every comment below either can, or must be left alone.

class SpaceAfterTripleSlashShapes {
    ///<summary>A lone element, already what the markerless rendering writes.</summary>
    int Lone() => 0;

    ///Plain text with no element.
    int Text() => 0;

    /// <summary>
    ///     <para>Indented content under markerless lines.</para>
    /// </summary>
    int MarkerlessIndented() => 0;

    /// <summary>
    ///     Markerless, with a break between a word and a sibling element,
    ///     <see cref="Lone" />, which the oracle keeps only in the configured convention.
    /// </summary>
    int BesideAnElement() => 0;

    /// <summary>
    ///     Markerless, with the element at the end of a line <see cref="Lone" />
    ///     and a word after the break, rebuilt for the same reason.
    /// </summary>
    int ElementThenWord() => 0;

    /// <summary>
    ///     <para>Sibling elements.</para>
    ///     <para>Kept: element beside element is not the exception.</para>
    /// </summary>
    int SiblingElements() => 0;

    /// <summary>
    ///     <para>Continuation lines with no content indentation.</para>
    ///     Some text.
    /// </summary>
    int Continuation() => 0;

    /// <summary>Lines that disagree about the marker.</summary>
    /// <returns>A value.</returns>
    int Mixed() => 0;

    /// <summary>The other way round.</summary>
    /// <returns>A value.</returns>
    int MixedReversed() => 0;

    /// <summary>
    ///     <para>Markerless outer lines, content indented one column too far.</para>
    /// </summary>
    int MarkerlessOverIndented() => 0;

    /// <summary>A blank line between tags.</summary>
    /// <returns>A value.</returns>
    int Blank() => 0;

    ///
    int LoneBlank() => 0;

    ////<summary>Four slashes are a line comment.</summary>
    int FourSlashes() => 0;

    /// <summary>One space.</summary>
    int OneSpace() => 0;

    /// <summary>A tab.</summary>
    int Tab() => 0;

    /// <summary>Two spaces.</summary>
    int TwoSpaces() => 0;

#if SKALA_NEVER_DEFINED
    ///<summary>Inside an inactive region.</summary>
    int Inactive() => 0;
#endif
}
