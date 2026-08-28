using System.Security.Cryptography;
using System.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;

namespace Rikarin.Skala.Testing;

/// <summary>
///     Skala's half of a key-flip measurement: one fixture, one option, one value.
/// </summary>
/// <remarks>
///     ⚠ <b>Why this is here and not in the sweep.</b> Two things now have to produce byte-identical
///     answers from the same three inputs: <c>KeyFlipSweep</c>, which measures against the oracle and
///     writes the committed table, and <c>ProvenanceTests.TheCommittedSweep_MeasuredTheFormatterInForce</c>,
///     which re-asks Skala alone and fails when the answer has moved. If those two ever drift apart the
///     drift test reports a formatter change on every run and means nothing.
///     <para>
///         ⚠ The repository has been bitten by exactly this: <c>OptionDomain</c>'s remarks record five
///         hand-kept copies of "the legal values of an option", four of which were invalidated at once by
///         giving int options a minimum. One implementation, two callers.
///     </para>
/// </remarks>
public static class SkalaSide {
    /// <summary>
    ///     Skala's answer for one option at one value, resolved from the repository's own chain.
    /// </summary>
    /// <remarks>
    ///     ⚠ Resolved from the fixture's real path and not from a copy in a scratch tree, which is both
    ///     cheaper and safer: <c>ConfigurationCache</c> memoises a parsed <c>.editorconfig</c> per path
    ///     with no eviction, and a fresh 294 KB copy per (option, value) would fill it with about a
    ///     thousand parses of the same document.
    ///     <para>
    ///         ⚠ Raw, and deliberately not normalised. Normalising here and not on the oracle side made
    ///         <c>resharper_csharp_insert_final_newline</c> look <c>INERT</c> — the oracle moving and Skala
    ///         not — when <c>skala format --option</c> on the same fixture writes 12 bytes at <c>true</c>
    ///         and 11 at <c>false</c>. Both engines are asked the same question in the same units, and the
    ///         comparison normalises them together.
    ///     </para>
    /// </remarks>
    public static string Format(string fixturePath, string key, string value) =>
        Format(fixturePath, [new KeyValuePair<string, string>(key, value)]);

    /// <summary>
    ///     The same, with more than one key forced at once — the pairwise pass's grid.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>OptionResolver</c> applies the overrides last and in order, which is how both engines
    ///     reach the same configuration: the oracle is handed an appended <c>[*.cs]</c> section carrying
    ///     the same assignments in the same order, and an <c>.editorconfig</c>'s last assignment of a key
    ///     wins. A pair that assigned the same key twice would therefore be measuring its second value
    ///     only, which is why the pairwise plan refuses to pair a key with itself.
    /// </remarks>
    public static string Format(string fixturePath, IReadOnlyList<KeyValuePair<string, string>> overrides) {
        var resolved = OptionResolver.Resolve(fixturePath, overrides);

        // ⚠ A value error is an answer, not an exception. A probe set is built from the registry's
        // declared domain and the resolver may still refuse a value — an int outside its bounds, an
        // enum spelling the registry lists and the parser does not — and a run that threw on the
        // first one would measure nothing. Recording the refusal as the output keeps it comparable
        // across runs, and makes a newly-refused value show up as drift rather than as a crash.
        if (!resolved.ValueErrors.IsEmpty) {
            return "value-error: " + string.Join("; ", resolved.ValueErrors);
        }

        var text = CSharpFormatter.Read(fixturePath);
        return CSharpFormatter.Format(fixturePath, text, resolved.Options).Formatted;
    }

    /// <summary>A short digest of one engine's output, so a table can be read as a diff.</summary>
    /// <remarks>
    ///     ⚠ Eight hex characters. Short enough to sit in a markdown column, and the population it has to
    ///     separate is the handful of distinct outputs one option produces across its own values — not a
    ///     corpus. It is a comparison aid and never a security claim.
    /// </remarks>
    public static string Digest(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..8];
}
