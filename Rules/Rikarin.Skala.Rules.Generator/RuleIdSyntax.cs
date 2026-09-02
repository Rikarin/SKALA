namespace Rikarin.Skala.Rules.Generator;

/// <summary>The shape of a rule id, as both generators in this assembly have to check it.</summary>
/// <remarks>
///     ⚠ Shared rather than duplicated: the check stood byte for byte identical in
///     <c>RulesGenerator</c> (validating <c>rules.json</c>) and <c>TaintGenerator</c> (validating the
///     <c>rule</c> each <c>taint.json</c> sink names). The two files are the only writers of the
///     <c>SK</c> namespace, so a copy that drifted would let one of them admit an id the other rejects
///     and the mismatch would surface as a missing descriptor at analysis time, not as a build error.
///     <para>
///         ⚠ Hand-written rather than a <c>Regex</c>: this assembly is a source generator and runs
///         inside the compiler on every keystroke in an IDE, so it avoids the regex engine's startup
///         cost on a check this simple. ADR-012 fixes the format at <c>SK</c> plus four digits, and it
///         is append-only, so the shape cannot widen later.
///     </para>
/// </remarks>
internal static class RuleIdSyntax {
    /// <summary>Whether <paramref name="id" /> is <c>SK</c> followed by exactly four digits.</summary>
    public static bool IsRuleId(string id) {
        if (id.Length != 6 || id[0] != 'S' || id[1] != 'K') {
            return false;
        }

        for (var i = 2; i < 6; i++) {
            if (id[i] < '0' || id[i] > '9') {
                return false;
            }
        }

        return true;
    }
}
