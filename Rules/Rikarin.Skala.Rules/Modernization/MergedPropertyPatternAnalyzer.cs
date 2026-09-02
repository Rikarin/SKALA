using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1123</c> — two property patterns joined by <c>or</c> that test the same property.
/// </summary>
/// <remarks>
///     <c>x is { Status: Draft } or { Status: Review }</c> asks one question about one property and
///     spells it as two questions about two objects. <c>{ Status: Draft or Review }</c> is the same
///     predicate with the subject written once, and it is the form the property extends to — a third
///     alternative adds a word rather than a clause.
///     <para>
///         ⚠
///         <b>
///             The merge is exact and needs no semantic model, because the shape it matches has no
///             type in it.
///         </b> A property pattern with no type resolves its member against the pattern's
///         <em>input</em> type, which is the same input on both sides of an <c>or</c>. Admitting a
///         type — <c>Draft { Status: A } or Review { Status: B }</c> — would be two different
///         members that happen to share a name, so both sides are required to be typeless and the
///         rule stays <c>Syntax</c>.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The designation guard this rule was written with is refuted, and the compiler is what
///             refutes it.
///         </b> CS8780 — "a variable may not be declared within a 'not' or 'or' pattern" —
///         makes <c>{ A: int i } or { A: 2 }</c> and <c>{ A: 1 } x or { A: 2 }</c> both uncompilable,
///         confirmed by compiling both. No program this rule can run on carries a pattern variable
///         there, so the guard was unreachable; and the one designation that <em>is</em> legal under
///         <c>or</c> is a discard, which merges perfectly well — <c>{ A: int _ } or { A: 2 }</c> and
///         its merged <c>{ A: int _ or 2 }</c> both compile. The guard's only reachable effect was to
///         decline a finding it should report.
///     </para>
///     <para>
///         ⚠ <b>No parentheses are needed and that is a property of the grammar, not luck.</b>
///         <c>or</c> is the loosest pattern combinator, so a subpattern that is itself an <c>and</c>
///         or a relational run keeps its meaning when it becomes an operand of a new <c>or</c>:
///         <c>{ A: &gt; 1 and &lt; 5 } or { A: 9 }</c> merges to <c>{ A: &gt; 1 and &lt; 5 or 9 }</c>,
///         which still groups the <c>and</c> first.
///     </para>
///     <para>
///         ⚠ Disjoint from <c>SK1011</c> by construction: that rule is registered on
///         <c>&amp;&amp;</c> only and its output is a single property pattern with one subpattern and
///         no <c>or</c>, so neither rule can consume the other's result.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MergedPropertyPatternAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.MergedPropertyPattern);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.MergedPropertyPattern);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.OrPattern);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var pattern = (BinaryPatternSyntax)context.Node;

        if (!TryReadSingleProperty(pattern.Left, out var leftName, out var leftValue)
            || !TryReadSingleProperty(pattern.Right, out var rightName, out var rightValue)
            || !string.Equals(leftName, rightName, System.StringComparison.Ordinal)) {
            return;
        }

        if (RewriteGuards.ContainsCommentOrDirective(pattern.SyntaxTree, pattern.Span)) {
            return;
        }

        var replacement = "{ " + leftName + ": " + leftValue + " or " + rightValue + " }";
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                pattern.GetLocation(),
                FixEdits.Pack((pattern.Span, replacement)),
                "Both alternatives test `" + leftName + "`: `" + RewriteGuards.Trim(replacement) + "`"
            )
        );
    }

    /// <summary>
    ///     A typeless property pattern testing exactly one property, with nothing else in it.
    /// </summary>
    static bool TryReadSingleProperty(PatternSyntax pattern, out string? name, out PatternSyntax? value) {
        name = null;
        value = null;

        if (Unwrap(pattern) is not RecursivePatternSyntax {
                Type: null,
                PositionalPatternClause: null,
                Designation: null,
                PropertyPatternClause.Subpatterns.Count: 1
            } recursive) {
            return false;
        }

        var subpattern = recursive.PropertyPatternClause!.Subpatterns[0];

        // ⚠ Only a plain `Name:` clause. An extended property pattern (`A.B: 1`) has an
        // `ExpressionColon` whose path may differ between the two sides in ways a name comparison
        // would not see, and merging two different paths is a different predicate.
        if (subpattern.NameColon is not { } colon) {
            return false;
        }

        // ⚠ There is no designation guard, and the guard that was written here is refuted rather
        // than merely unused. CS8780 — "a variable may not be declared within a 'not' or 'or'
        // pattern" — means no compiling program can put a pattern variable under the `or` this rule
        // matches, on either the subpattern or the recursive pattern; both were compiled and both
        // are errors. So the merged form is available whenever the source form exists, and a guard
        // would only have declined the one designation that *is* legal there — a discard, where
        // `{ A: int _ } or { A: 2 }` compiles and so does the `{ A: int _ or 2 }` it merges to.
        name = colon.Name.Identifier.ValueText;
        value = subpattern.Pattern;
        return true;
    }

    static PatternSyntax Unwrap(PatternSyntax pattern) {
        while (pattern is ParenthesizedPatternSyntax parenthesized) {
            pattern = parenthesized.Pattern;
        }

        return pattern;
    }
}
