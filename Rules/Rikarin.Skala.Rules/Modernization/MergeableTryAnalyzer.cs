using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1121</c> — a <c>try</c>/<c>catch</c> that is the entire body of a <c>try</c>/<c>finally</c>.
/// </summary>
/// <remarks>
///     ⚠ <b>Only one of the two nestings can be merged, and issue #109's own headline example is the
///     other one.</b> The issue proposes
///     <c>try { try { … } finally { … } } catch { … }</c> → one statement. Compiled and run, that
///     rewrite reverses the order of two side effects:
///     <code>
/// try { try { A } finally { F } } catch { C }    // body -&gt; finally -&gt; catch
/// try { A } catch { C } finally { F }            // body -&gt; catch   -&gt; finally
///     </code>
///     .NET's two-pass exception handling runs the inner <c>finally</c> while unwinding to a handler
///     that has already been located, so <c>F</c> precedes <c>C</c>; a merged <c>finally</c> runs
///     after its own <c>catch</c>. ⚠ <b>ReSharper's inspection describes the sound nesting and the
///     issue transcribed it backwards</b> — the export reads
///     <c>"try-catch and try-finally statements can be merged"</c>, which is <c>catch</c> on the
///     <em>inner</em> statement and <c>finally</c> on the outer.
///     <para>
///         That direction is exact, and the reason is that an outer <c>finally</c> already covers the
///         inner <c>catch</c> bodies: in both spellings the <c>finally</c> runs after the handler,
///         whether the handler completes, returns or rethrows. Two runs of a rethrowing handler
///         confirm it — <c>body → catch → finally → escaped</c> either way.
///     </para>
///     <para>
///         ⚠ <b>An outer <c>catch</c> is never merged, and that is the same finding from the other
///         side.</b> Sibling <c>catch</c> clauses do not chain: where the nested form lets an
///         exception thrown <em>by</em> the inner handler reach the outer one, the merged form lets it
///         escape. Measured: <c>body → inner → outer</c> becomes <c>body → inner → escaped</c>.
///         <c>SK0240</c> owns the neighbouring question of a <c>catch</c> that only rethrows.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MergeableTryAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.MergeableTry);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.MergeableTry);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.TryStatement);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var outer = (TryStatementSyntax)context.Node;

        // ⚠ The outer statement must be a `try`/`finally` and nothing else. An outer `catch` makes
        // the merge unsound in the direction measured above: the nested form routes an exception
        // thrown by the inner handler to the outer clause, and sibling clauses do not.
        if (outer.Catches.Count != 0 || outer.Finally is not { } tail) {
            return;
        }

        // ⚠ Exactly one statement, because the outer `finally` covers every statement in its block
        // and the merged form only covers the inner `try`'s. A second statement beside the inner
        // `try` is silently dropped out of the protected region by the rewrite.
        if (outer.Block.Statements.Count != 1 || outer.Block.Statements[0] is not TryStatementSyntax inner) {
            return;
        }

        // ⚠ The inner statement must carry the handlers and no `finally` of its own. Merging two
        // `finally` blocks concatenates two statement lists into one scope, where a local declared
        // in each collides, and where a throw from the first stops the second running at all.
        if (inner.Catches.Count == 0 || inner.Finally is not null) {
            return;
        }

        // The two regions the edit deletes: the outer `try {` before the inner statement, and the
        // outer block's `}` between the inner statement and the `finally`.
        var head = TextSpan.FromBounds(outer.SpanStart, inner.SpanStart);
        var neck = TextSpan.FromBounds(inner.Span.End, tail.SpanStart);

        // ⚠ Asked over exactly the two spans that vanish, never over the node (#302): a comment
        // above the statement, or inside the body or a handler, is text this edit keeps.
        if (RewriteGuards.ContainsCommentOrDirective(outer.SyntaxTree, head)
            || RewriteGuards.ContainsCommentOrDirective(outer.SyntaxTree, neck)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                outer.TryKeyword.GetLocation(),
                FixEdits.Pack((head, string.Empty), (neck, " ")),
                "The `finally` belongs on the `try` that already has the handlers"
            )
        );
    }
}
