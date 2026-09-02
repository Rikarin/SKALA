using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1101</c> — a local declared on one line and given its value on the next.
/// </summary>
/// <remarks>
///     <c>int count; count = Read();</c> is the C-era habit that every model has read a great deal of,
///     and it costs a reader the one guarantee the joined form gives for free: that the variable is
///     never observable before it holds something. The rewrite is the smallest one in this batch —
///     two lines become one and no expression moves.
///     <para>
///         ⚠ <b>Only the joining half of issue #83 ships, and cutting the other half was a decision
///         rather than an omission.</b> The issue also asks for <c>TooWideLocalVariableScope</c> —
///         moving a declaration <em>into</em> the narrower block that uses it. That rewrite moves a
///         declaration <em>inwards</em>, which is the one direction <see cref="RewriteGuards" /> cannot
///         check: <c>WouldCollide</c> and <c>DeclaredElsewhereInMember</c> both answer the outward
///         question, and #304 is a rule from this session that emitted a token-equivalent program
///         failing <c>CS0136</c> for exactly that blind spot. Shipping half a concept with a guard is
///         worth more than shipping all of it with a fix that breaks builds.
///     </para>
///     <para>
///         ⚠ <b>No semantic model is needed and that is a fact about C# lookup, not a shortcut.</b> An
///         identifier written immediately after <c>T x;</c> in the same statement list resolves to that
///         local — a simple name finds the innermost enclosing declaration first, and nothing can be
///         declared between two adjacent statements. So the rule runs under <c>--load=loose</c>, which
///         is the mode an agent's scratch file is analysed in and exactly where this shape arrives.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SplitDeclarationAndAssignmentAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SplitDeclarationAndAssignment);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LocalDeclarationStatement);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (LocalDeclarationStatementSyntax)context.Node;
        if (declaration.UsingKeyword != default
            || declaration.AwaitKeyword != default
            || declaration.Modifiers.Count > 0
            || declaration.AttributeLists.Count > 0
            || declaration.Declaration.Type is RefTypeSyntax
            || declaration.Declaration.Variables.Count != 1) {
            return;
        }

        // ⚠ Two declarators are two findings that overlap: `int a, b; a = 1;` joins one and leaves
        // the other where it was, and the text edit for that is a different rewrite.
        var declarator = declaration.Declaration.Variables[0];
        if (declarator.Initializer is not null || declarator.ArgumentList is not null) {
            return;
        }

        if (StatementRewrites.Next(declaration) is not ExpressionStatementSyntax {
                Expression: AssignmentExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                    Left: IdentifierNameSyntax left,
                    Right: { } value
                }
            } assignment) {
            return;
        }

        var name = declarator.Identifier.ValueText;
        if (!string.Equals(left.Identifier.ValueText, name, System.StringComparison.Ordinal)) {
            return;
        }

        // ⚠ `int x; x = Parse(s, out x);` is legal and `int x = Parse(s, out x);` is not — a local
        // may not appear in its own initializer at all, `out` position included. Any mention of the
        // name inside the value withdraws the finding; `nameof(x)` is caught with it, which costs a
        // finding nobody will miss.
        foreach (var node in value.DescendantNodesAndSelf()) {
            if (node is IdentifierNameSyntax mention
                && string.Equals(mention.Identifier.ValueText, name, System.StringComparison.Ordinal)) {
                return;
            }
        }

        var tree = declaration.SyntaxTree;
        var text = tree.GetText();

        // Everything from the declared name to the end of the assignment collapses into ` = value;`,
        // so a comment on the line between them is deleted unless the finding withdraws.
        var replaced = TextSpan.FromBounds(declarator.Identifier.Span.End, assignment.Span.End);
        if (StatementRewrites.DeletesAuthoredText(tree, replaced, value.Span)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(tree, declarator.Identifier.Span),
                FixEdits.Pack((replaced, " = " + text.ToString(value.Span) + ";")),
                "`" + name + "` is declared here and assigned on the next line"
            )
        );
    }
}
