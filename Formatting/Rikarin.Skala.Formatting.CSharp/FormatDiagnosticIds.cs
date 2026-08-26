namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
/// The formatter's slice of the SK9000 range. ⚠ ADR-012: an id is allocated once and never redefined.
/// </summary>
public static class FormatDiagnosticIds {
    /// <summary>A line exceeded the width and nothing could break. Hint; the audit only.</summary>
    public const string LineTooLong = "SK0002";

    /// <summary>The file does not parse. Reported, left byte-identical, never formatted (ADR-003).</summary>
    public const string NotParseable = "SK9010";

    /// <summary>A member's braces are split across a preprocessor branch; it is emitted verbatim.</summary>
    public const string UnbalancedPreprocessor = "SK9011";

    /// <summary>
    /// ⚠ The token stream of the output differs from the input's. A Skala bug by definition: the
    /// file is abandoned, nothing is written, and a reproduction is dropped under
    /// <c>.skala/crash/</c>. There is no flag that turns the check off.
    /// </summary>
    public const string TokenStreamChanged = "SK9099";
}
