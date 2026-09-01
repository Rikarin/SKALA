using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1043</c> — a <c>for</c> with nothing but a condition is a <c>while</c>.
/// </summary>
/// <remarks>
///     The two empty clauses are the finding: <c>for</c> promises a bound and then declares none, so a
///     reader has to check both semicolons to learn there is nothing there. The rewrite is exact —
///     with no incrementors, <c>continue</c> jumps to the condition in both forms.
///     <para>
///         ⚠ <c>for (;;)</c> is deliberately <b>not</b> reported. It is the idiomatic infinite loop,
///         not a <c>while</c> that lost its condition, and <c>while (true)</c> says nothing the
///         original did not. The rule requires a condition to be present.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ForAsWhileAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ForLoopIsWhile);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ForStatement);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var loop = (ForStatementSyntax)context.Node;
        if (loop.Declaration is not null
            || loop.Initializers.Count > 0
            || loop.Incrementors.Count > 0
            || loop.Condition is not { } condition) {
            return;
        }

        var tree = loop.SyntaxTree;
        var header = TextSpan.FromBounds(loop.ForKeyword.SpanStart, loop.CloseParenToken.Span.End);

        // The header is replaced wholesale except for the condition, so anything a person wrote in
        // the empty clauses would be deleted.
        if (RewriteGuards.ContainsCommentOrDirective(tree, TextSpan.FromBounds(header.Start, condition.SpanStart))
            || RewriteGuards.ContainsCommentOrDirective(tree, TextSpan.FromBounds(condition.Span.End, header.End))) {
            return;
        }

        var text = "while (" + tree.GetText().ToString(condition.Span) + ")";
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(tree, header),
                FixEdits.Pack((header, text)),
                "The `for` loop is a `while`: `" + RewriteGuards.Trim(text) + "`"
            )
        );
    }
}
