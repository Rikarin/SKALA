// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// #587: two elements glued together are a break point for width; an element glued to a word is not.

class GluedElements {
    /// <remarks>
    ///     <list>
    ///         <item>A.</item><item>B.</item>
    ///     </list>
    /// </remarks>
    void M0() { } // short glued items

    /// <remarks>
    ///     <list>
    ///         <item>One item.</item>
    ///         <item>Another item, written at enough length that the list cannot fit on any single line of its own.</item>
    ///     </list>
    /// </remarks>
    void M1() { } // overflowing glued items

    /// <remarks>
    ///     <list type="table">
    ///         <item>
    ///             <term>textDocument/rangeFormatting</term>
    ///             <description>full-file fit, edits filtered to the range and some</description>
    ///         </item>
    ///     </list>
    /// </remarks>
    void M2() { } // term then description

    /// <remarks>
    ///     <list type="table">
    ///         <item>
    ///             <term>short</term><description>short</description>
    ///         </item>
    ///     </list>
    /// </remarks>
    void M3() { } // term description short

    /// <summary>
    ///     Prose that runs along for quite some distance before it reaches two glued codes <c>alphaalpha</c>
    ///     <c>betabetabeta</c> end.
    /// </summary>
    void M4() { } // glued inline codes at the margin

    /// <summary>
    ///     Prose that runs along for quite some distance before it reaches two glued refs <see cref="A" />
    ///     <see cref="BBBBBB" /> end.
    /// </summary>
    void M5() { } // glued sees at the margin

    /// <summary>
    ///     Prose that runs along for quite some distance before it reaches a glued tail <c>alphaalphaalpha</c>betabeta end.
    /// </summary>
    void M6() { } // element glued to a word at the margin
}
