// #569: a <code> whose content spans lines is written back byte for byte from its start tag to its end tag —
// code on the tag's line, an end tag glued to the last line, and the whitespace before one on a line of its own.
class CodeBlockEdges {
    /// <example>
    /// Text:
    /// <code>
    /// var a = 1;
    ///     a++;
    /// </code>
    /// </example>
    void M0() { } // end tag at 0, start tag moved to 4

    /// <example>
    ///     <code>
    /// var a = 1;
    ///         </code>
    /// </example>
    void M1() { } // end tag at 8

    /// <example>
    /// <code>
    /// var a = 1;
    ///   </code>
    /// </example>
    void M2() { } // end tag at 2

    /// <remarks>
    /// <para>
    /// <code>
    /// var a = 1;
    /// </code>
    /// </para>
    /// </remarks>
    void M3() { } // nested, end tag at 0

    /// <remarks>
    /// <code>
    /// var a = 1;
    /// a++;</code>
    /// </remarks>
    void M4() { } // end tag after content

    /// <remarks>
    /// <code>var a = 1;
    /// a++;
    /// </code>
    /// </remarks>
    void M5() { } // content on the start tag's line

    /// <summary>Text.</summary>
    /// <example>
    /// <code>
    /// var a = 1;
    /// </code>
    /// Trailing.
    /// </example>
    void M6() { } // prose after, end tag at 0

    /// <example>
    ///     <code>
    ///     var a = 1;
    ///     </code>
    /// </example>
    void M7() { } // all at 4

    /// <example>
    /// <code>
    /// var a = 1;
    /// </code></example>
    void M8() { } // end tags glued
}
