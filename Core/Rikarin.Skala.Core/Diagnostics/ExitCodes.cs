namespace Rikarin.Skala.Core.Diagnostics;

/// <summary>
///     docs/plan/09 § "Exit codes" (ADR-010). Fixed, documented, and depended upon by hooks, CI and
///     agents.
/// </summary>
/// <remarks>
///     ⚠ It lives in Core, and that is the whole point. Until this was consolidated the table existed
///     twice: here, and as <c>FormatCommand.ChangesFound</c>/<c>FormatCommand.Failed</c> in
///     <c>Rikarin.Skala.Formatting.CSharp</c>, which cannot see <c>Rikarin.Skala.Reporting</c> where
///     the table used to live. The two copies disagreed — <c>format --check</c> returned <b>1</b> for
///     "there are edits" and <b>2</b> for "a file failed", the exact inverse of the documented
///     contract — and the disagreement survived from M1 to M9 because nothing compared them. A contract
///     two assemblies must agree on belongs in the assembly they both reference.
///     <para>
///         ⚠ There used to be an exception: the NativeAOT thin client repeated these numbers as literals,
///         because it referenced neither Core nor Roslyn on purpose, and <c>ClientAgreesWithToolTests</c>
///         ran both binaries and compared the codes to hold the copy in step. The client is gone with the
///         daemon, so there is no copy left and there is nothing to compare.
///     </para>
/// </remarks>
public static class ExitCodes {
    /// <summary>The gate passed. Findings may exist below it.</summary>
    public const int Ok = 0;

    /// <summary>The gate failed.</summary>
    public const int GateFailed = 1;

    /// <summary>
    ///     ⚠ Formatting changes are needed — <c>format --check</c>, <c>arrange --check</c>.
    /// </summary>
    /// <remarks>
    ///     Distinct from <see cref="GateFailed" /> on purpose: a hook that auto-formats on 2 and stops
    ///     on 1 is a two-line hook, and that is the only reason the two codes are not one.
    /// </remarks>
    public const int FormattingNeeded = 2;

    /// <summary>
    ///     A configuration or usage error: <c>SK9001</c>–<c>SK9005</c>, an unparseable option value, or
    ///     an invocation the tool refuses (<c>--staged</c> outside a git repository).
    /// </summary>
    public const int ConfigurationError = 3;

    /// <summary>No compilation could be built.</summary>
    /// <remarks>
    ///     ⚠ "None", and every branch in the tree that reads this code takes it literally:
    ///     <c>VerifyCommand.Verdict</c> returns a 4 without a partial verdict because there is no
    ///     report to be partial about, and <c>fix</c> and <c>baseline</c> stop on it. That is why an
    ///     unreadable source file (<c>SK9015</c>) is <b>not</b> this code — see
    ///     <see cref="InternalError" />.
    /// </remarks>
    public const int LoadFailure = 4;

    /// <summary>
    ///     Internal error, including <c>SK9099</c>: the formatter's safety net tripping on a file, or
    ///     an I/O failure that stopped a file being read or written.
    /// </summary>
    /// <remarks>
    ///     ⚠ #357 decided that <c>SK9015</c> stays here rather than moving to
    ///     <see cref="LoadFailure" />, and refuted the premise that 5 means "this is a Skala bug".
    ///     That sentence is what <c>Program.cs</c> prints for an <em>unhandled exception</em>; the
    ///     clause above about an I/O failure predates #353 by two weeks (<c>3578e170</c>). No consumer
    ///     outside the process — the MSBuild target, the MCP verdict, the pre-commit hook, CI —
    ///     distinguishes 4 from 5, and every consumer inside it reads 4 as "there is no report", which
    ///     an unreadable file beside a checked tree is not. docs/plan/10 § "The INCOMPLETE banner"
    ///     holds the survey.
    /// </remarks>
    public const int InternalError = 5;

    public const int Cancelled = 130;
}
