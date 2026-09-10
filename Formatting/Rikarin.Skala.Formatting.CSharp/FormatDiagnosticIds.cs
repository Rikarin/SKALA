namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
///     The formatter's slice of the SK9000 range. ⚠ ADR-012: an id is allocated once and never redefined.
/// </summary>
public static class FormatDiagnosticIds {
    /// <summary>A line exceeded the width and nothing could break. Hint; the audit only.</summary>
    public const string LineTooLong = "SK0002";

    /// <summary>
    ///     A documentation comment is not well-formed XML. Hint; it is left exactly as written.
    /// </summary>
    /// <remarks>
    ///     ⚠ Never "fixed" (docs/plan/05 § "Phase 4"). Malformed doc comments are extremely common in
    ///     real code — an unescaped <c>&lt;</c>, a <c>&lt;br&gt;</c> borrowed from HTML, a tag somebody
    ///     forgot to close — and a formatter that repairs them is a formatter that changes what the
    ///     documentation says.
    /// </remarks>
    public const string MalformedXmlDoc = "SK0003";

    /// <summary>The file does not parse. Reported, left byte-identical, never formatted (ADR-003).</summary>
    public const string NotParseable = "SK9010";

    /// <summary>A member's braces are split across a preprocessor branch; it is emitted verbatim.</summary>
    public const string UnbalancedPreprocessor = "SK9011";

    /// <summary>The file could not be read or written. ⚠ Not a formatting failure — an I/O one.</summary>
    /// <remarks>
    ///     ⚠ Both call sites used a bare <c>"SK9012"</c> literal, which is `SkalaDiagnostic`'s
    ///     canonical-version id. Two meanings behind one number, and the ADR-012 guard missed it
    ///     because it read <em>declarations</em> and these were <em>uses</em>.
    ///     <para>
    ///         ⚠ This is also the answer for a file the process is not permitted to read (#353), and
    ///         deliberately not <c>SK9098</c>: a mode-600 file owned by someone else is not a Skala
    ///         bug. The contract is the one <c>SK9010</c> already sets for a file that does not
    ///         parse — report it, leave it exactly as it was, keep going, exit non-zero.
    ///     </para>
    /// </remarks>
    public const string FileIoFailed = "SK9015";

    /// <summary>
    ///     ⚠ The token stream of the output differs from the input's. A Skala bug by definition: the
    ///     file is abandoned, nothing is written, and a reproduction is dropped under
    ///     <c>.skala/crash/</c>. There is no flag that turns the check off.
    /// </summary>
    /// <remarks>
    ///     ⚠ The summary above sat on <see cref="FileIoFailed" /> until #353, stacked as a second
    ///     <c>&lt;summary&gt;</c> over that member's own — so the one id that means "this IS a Skala
    ///     bug" was documented as the one that means "this is not", and this member had no docs at
    ///     all. Found while establishing that an unreadable file must not be reported as a bug.
    /// </remarks>
    public const string TokenStreamChanged = "SK9099";
}
