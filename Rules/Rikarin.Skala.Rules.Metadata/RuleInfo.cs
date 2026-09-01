using System.Collections.Generic;

namespace Rikarin.Skala.Rules.Metadata;

/// <summary>docs/plan/03-configuration-model.md § "Severities" — ReSharper's five levels.</summary>
public enum RuleSeverity {
    /// <summary>Suppressed. Never runs, never reported.</summary>
    None,

    /// <summary>Roslyn <c>Hidden</c>. Only shown with <c>--include-hints</c>.</summary>
    Hint,

    /// <summary>Roslyn <c>Info</c>. Shown, dimmed; never fails a gate.</summary>
    Suggestion,

    /// <summary>Roslyn <c>Warning</c>. Fails a gate depending on the gate.</summary>
    Warning,

    /// <summary>Roslyn <c>Error</c>. Always fails a gate.</summary>
    Error
}

/// <summary>
///     What a rule needs in order to answer, and what the incremental cache may assume about it.
/// </summary>
/// <remarks>
///     ⚠ The two consumers are different questions and it is worth keeping them distinct in the head.
///     <list type="bullet">
///         <item>
///             <see cref="Syntax" /> versus <see cref="Semantic" /> decides whether the rule runs under
///             <c>--load=loose</c>, where most type resolution fails (docs/plan/07 § "loose").
///         </item>
///         <item>
///             <see cref="Compilation" /> decides whether the rule may be cached per file. ⚠ It may not: the
///             cache's correctness condition is that a rule's output for a file depends only on the key's
///             inputs, and a rule that reads every file in the compilation violates it. Getting this wrong
///             produces stale findings, which is the failure mode that destroys trust in a cache permanently
///             and does it silently.
///         </item>
///     </list>
/// </remarks>
public enum RuleScope {
    /// <summary>The syntax tree of one file answers it.</summary>
    Syntax,

    /// <summary>One file plus the semantic model answers it.</summary>
    Semantic,

    /// <summary>⚠ It reads the whole compilation. Excluded from per-file caching.</summary>
    Compilation
}

/// <summary>
///     One rule, as <c>rules.json</c> declares it.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "Rule metadata". The single source for the analyzer's
///     <c>DiagnosticDescriptor</c>, the <c>docs/rules/</c> page, the <c>skala explain</c> text, the
///     SARIF <c>rules[]</c> block and the ReSharper severity mapping.
/// </remarks>
public sealed record RuleInfo(
    string Id,
    string Concept,
    string Title,
    string Category,
    RuleSeverity DefaultSeverity,
    RuleScope Scope,
    bool RequiresSemantics,
    bool HasFix,
    bool FixIsSafe,
    bool Retired,
    string? ReSharperId,
    IReadOnlyList<string> Supersedes,
    string Since,
    string? LanguageVersion,
    string Summary,
    string Rationale,
    string BadExample,
    string GoodExample,
    string FalsePositives,
    IReadOnlyList<string> Configuration,
    string? ReSharperNote) {
    /// <summary>
    ///     The <c>resharper_*_highlighting</c> key this rule's severity can be read from, or null.
    /// </summary>
    /// <remarks>
    ///     ⚠ Derived, not stored, and the derivation is the answer to docs/plan/16 § Q5. ReSharper's
    ///     key is its inspection id in snake_case with a <c>resharper_</c> prefix and a
    ///     <c>_highlighting</c> suffix — <c>ConvertToFileScopedNamespace</c> becomes
    ///     <c>resharper_convert_to_file_scoped_namespace_highlighting</c> — so the mapping table is one
    ///     field per rule rather than a second file to keep in sync.
    /// </remarks>
    public string? ReSharperSeverityKey =>
        ReSharperId is null ? null : "resharper_" + SnakeCase(ReSharperId) + "_highlighting";

    /// <summary>Whether the rule can run at all under <c>--load=loose</c>.</summary>
    public bool RunsWithoutAProject => !RequiresSemantics && Scope != RuleScope.Compilation;

    /// <summary>⚠ Compilation-scoped rules are never cached per file. See <see cref="RuleScope" />.</summary>
    public bool IsCacheable => Scope != RuleScope.Compilation;

    internal static string SnakeCase(string pascal) {
        var builder = new System.Text.StringBuilder(pascal.Length + 8);
        for (var i = 0; i < pascal.Length; i++) {
            var c = pascal[i];
            if (char.IsUpper(c)) {
                // ⚠ A run of capitals is one word: `ConvertToASCII` is `convert_to_ascii`, not
                // `convert_to_a_s_c_i_i`. ReSharper's own keys are written that way.
                var startsWord = i > 0
                    && (!char.IsUpper(pascal[i - 1]) || i + 1 < pascal.Length && char.IsLower(pascal[i + 1]));
                if (startsWord && builder.Length > 0 && builder[builder.Length - 1] != '_') {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            } else if (c == '.') {
                // ⚠ ReSharper's severity-scoped inspections are spelled `MemberCanBeMadeStatic.Global`
                // and `.Local`, and the exported key separates the two halves with an underscore like
                // any other word boundary. Without this the id derives to a key ending `._global`,
                // which JetBrains never emits — a mapping that looks like a feature and behaves like a
                // comment. EveryDeclaredReSharperKey_ExistsInTheExport is what catches that.
                if (builder.Length > 0 && builder[builder.Length - 1] != '_') {
                    builder.Append('_');
                }
            } else {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
