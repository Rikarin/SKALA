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
    public const int LoadFailure = 4;

    /// <summary>
    ///     Internal error, including <c>SK9099</c>: the formatter's safety net tripping on a file, or
    ///     an I/O failure that stopped a file being read or written.
    /// </summary>
    public const int InternalError = 5;

    public const int Cancelled = 130;
}
