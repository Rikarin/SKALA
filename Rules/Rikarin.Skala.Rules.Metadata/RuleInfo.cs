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
///     <c>DiagnosticDescriptor</c>, the <c>docs/rules/</c> page, the <c>skala explain</c> text and the
///     SARIF <c>rules[]</c> block.
///     <para>
///         ⚠ There is no longer a <c>ReSharperId</c>, and with it went the
///         <c>resharper_*_highlighting</c> severity bridge. One field named one inspection while a
///         rule routinely covers several, so <c>resharper_&lt;x&gt;_highlighting = none</c> either
///         silenced a rule covering ten other concepts or was inert for the other ten — it could not
///         mean what a reader expected. <see cref="ReSharperNote" /> stays: it is prose about how a
///         concept lines up against ReSharper's, not a machine-readable mapping.
///     </para>
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
    /// <summary>Whether the rule can run at all under <c>--load=loose</c>.</summary>
    public bool RunsWithoutAProject => !RequiresSemantics && Scope != RuleScope.Compilation;

    /// <summary>⚠ Compilation-scoped rules are never cached per file. See <see cref="RuleScope" />.</summary>
    public bool IsCacheable => Scope != RuleScope.Compilation;
}
