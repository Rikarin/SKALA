using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
///     The identifiers arrangement reports under. ⚠ Deliberately not <c>SK1xxx</c>: those are
///     modernization *rules with fixes* that <c>skala check</c> reports and a person opts into
///     (docs/plan/06 § "The modernization set"). These are cleanup settings that <c>skala arrange</c>
///     applies, and conflating the two produces the wall of suggestions doc 06 warns about.
/// </summary>
public static class ArrangeIds {
    public const string BodyStyle = "SK2001";
    public const string Var = "SK2002";
    public const string ObjectCreation = "SK2003";
    public const string DefaultValue = "SK2004";
    public const string NullCheckingPattern = "SK2005";
    public const string EmptyString = "SK2006";
    public const string ThisQualifier = "SK2007";
    public const string RedundantBraces = "SK2008";
    public const string RedundantParentheses = "SK2009";
    public const string Usings = "SK2010";
    public const string PredefinedType = "SK2011";
    public const string Accessibility = "SK2012";
    public const string NamespaceBody = "SK2013";
    public const string TrailingComma = "SK2014";
    public const string StaticQualifier = "SK2015";
    public const string ArgumentStyle = "SK2016";
    public const string DiscardDeclaration = "SK2017";

    /// <summary>⚠ A rewrite was reverted because re-binding produced a diagnostic it had not.</summary>
    public const string Reverted = "SK9098";

    /// <summary>⚠ A rewrite was reverted because an identifier in it resolved to a different symbol.</summary>
    public const string SymbolChanged = "SK9096";

    public static string NameOf(string id) =>
        id switch {
            BodyStyle => "body style",
            Var => "var",
            ObjectCreation => "target-typed new",
            DefaultValue => "default literal",
            NullCheckingPattern => "is not null",
            EmptyString => "empty string literal",
            ThisQualifier => "this qualifier",
            RedundantBraces => "redundant braces",
            RedundantParentheses => "redundant parentheses",
            Usings => "usings",
            PredefinedType => "predefined type",
            Accessibility => "redundant accessibility",
            NamespaceBody => "file-scoped namespace",
            TrailingComma => "trailing comma",
            StaticQualifier => "static member qualifier",
            ArgumentStyle => "argument style",
            DiscardDeclaration => "discard declaration",
            _ => id
        };
}

/// <summary>
///     What one rewriter is allowed to see, and where it records what it touched.
/// </summary>
/// <remarks>
///     ⚠ <see cref="Model" /> is null under <see cref="ArrangementScope.Syntactic" /> and a rewriter that
///     needs it must say so through <see cref="ArrangementRule.NeedsSemantics" /> rather than by
///     null-checking at the point of use. docs/plan/06 § "Safety" layer 1: a rewrite that cannot prove
///     its precondition does not run, and "the semantic model happened to be there" is not a proof.
/// </remarks>
public sealed class ArrangementContext {
    public ArrangementContext(
        SyntaxNode root,
        SemanticModel? model,
        in ArrangementOptions options,
        FormatterTagGuard? guard = null
    ) {
        Root = root;
        Model = model;
        Options = options;
        Guard = guard ?? FormatterTagGuard.Open;
    }

    public SyntaxNode Root { get; }

    public SemanticModel? Model { get; }

    public ArrangementOptions Options { get; }

    /// <summary>
    ///     The <c>@formatter:off</c> regions of <see cref="Root" />. Every rewriter of the catalogue
    ///     hands this to <see cref="GuardedRewriter" />; a rule that rebuilds nodes by hand instead —
    ///     <see cref="UsingsRule" /> — is caught by <see cref="FormatterTagGuard.PreservesAll" /> in
    ///     <see cref="Arranger" />.
    /// </summary>
    public FormatterTagGuard Guard { get; }

    /// <summary>
    ///     The model, or a throw. Only a rule whose <see cref="ArrangementRule.NeedsSemantics" /> is true
    ///     ever reaches this, and the driver does not run such a rule without one.
    /// </summary>
    public SemanticModel Semantics =>
        Model ?? throw new InvalidOperationException("a semantic rule ran without a semantic model");
}

/// <summary>One rewrite from docs/plan/06's catalogue.</summary>
public abstract class ArrangementRule {
    public abstract string Id { get; }

    /// <summary>
    ///     Whether the rule needs a <see cref="SemanticModel" />. ⚠ The syntactic subset is the one an
    ///     agent gets for free on a loose file (docs/plan/06 § "A few arrangements need no semantics").
    /// </summary>
    public abstract bool NeedsSemantics { get; }

    /// <summary>Whether the rule only runs under <c>--aggressive</c>.</summary>
    public virtual bool IsAggressive => false;

    /// <summary>Whether the configuration asks for this rule at all.</summary>
    public abstract bool IsEnabled(in ArrangementOptions options);

    /// <summary>
    ///     The rewritten root, or the same instance when there is nothing to do.
    /// </summary>
    /// <remarks>
    ///     ⚠ Returning the same instance rather than an equal one is the signal the driver uses, and it
    ///     is what keeps the fixed-point loop from spinning: <see cref="object.ReferenceEquals" /> on the
    ///     root is exact and free, while comparing two trees for equality is neither.
    /// </remarks>
    public abstract SyntaxNode Apply(ArrangementContext context);
}

/// <summary>What arranging one document produced.</summary>
public sealed record ArrangementResult(
    string Path,
    string Text,
    ImmutableArray<string> Applied,
    ImmutableArray<Core.Diagnostics.SkalaDiagnostic> Diagnostics,
    ArrangementOutcome Outcome) {
    public bool Changed => !Applied.IsEmpty;
}

/// <summary>How far the arranger got.</summary>
public enum ArrangementOutcome {
    /// <summary>Nothing wanted to change.</summary>
    Unchanged,

    Arranged,

    /// <summary>The file does not parse (ADR-003). Reported, left byte-identical.</summary>
    NotParseable,

    /// <summary>Generated code, skipped by policy.</summary>
    Generated,

    /// <summary>⚠ A safety layer rejected the result. Nothing was written.</summary>
    Reverted
}
