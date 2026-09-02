using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2173</c> — <c>is not { }</c>, which is <c>is null</c> spelled so that it reads as its
///     opposite.
/// </summary>
/// <remarks>
///     <c>{ }</c> matches every non-null value, so <c>not { }</c> matches every null one. The word on
///     the page is <c>not</c>, the reader takes it as "has something", and the pattern means "has
///     nothing".
///     <para>
///         ⚠ <b><c>{ }</c> matches a boxed struct and a non-null <c>Nullable&lt;T&gt;</c> as well as a
///         reference, and the rewrite keeps all of it</b> — <c>is null</c> on a <c>T?</c> is
///         <c>!HasValue</c>, which is exactly what <c>is not { }</c> was already testing.
///     </para>
///     <para>
///         ⚠ <b>The one shape that would break the rewrite cannot be written, and the compiler is what
///         guarantees it.</b> <c>int value</c> in <c>value is not { }</c> is <c>CS8518</c> — "an
///         expression of type 'int' can never match the provided pattern" — and so is a <c>T</c>
///         constrained to <c>struct</c>. There is no compiling program in which <c>is not { }</c> stands
///         on something <c>is null</c> would reject, so the rule needs no semantic model to know it.
///         Measured on a probe, not reasoned from the specification.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NegatedEmptyPatternAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NegatedEmptyPattern);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // ⚠ Registered on the `not` pattern itself rather than on `is`, so the rule reaches a
        // `case not { }:` label, a `switch` arm and a nested subpattern as well as a bare `is`.
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.NotPattern);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var not = (UnaryPatternSyntax)context.Node;

        // ⚠ Empty in all four ways. `not { Length: 0 }` has a subpattern, `not string { }` has a type,
        // `not (1, 2)` is positional and `not { } x` binds a designation; none of them is a null check.
        // A parenthesised `not ({ })` is a different node kind and is declined with them.
        if (not.Pattern is not RecursivePatternSyntax {
                Type: null,
                PositionalPatternClause: null,
                Designation: null,
                PropertyPatternClause: { } properties
            }
            || properties.Subpatterns.Count > 0) {
            return;
        }

        // ⚠ The finding is withdrawn rather than the fix, so that no positive fixture can produce a
        // report the fix cannot serve. The fix replaces the whole `not { }` span, and a fix that
        // silently deleted a comment out of that span would be a fix nobody can review.
        if (RewriteGuards.ContainsCommentOrDirective(not)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                not.GetLocation(),
                FixEdits.Pack((not.Span, "null")),
                "`not { }` matches exactly the null values, so this is `null` written backwards"
            )
        );
    }
}
