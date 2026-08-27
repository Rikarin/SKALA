using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
/// <c>SK1015</c> — <c>is T</c> followed by a cast to <c>T</c> is <c>is T t</c>.
/// </summary>
/// <remarks>
/// ⚠ M5 recorded this as a rule whose guard is most of the work, because "followed by a cast"
/// covers a cast anywhere in the body and the fix would then have to invent a name, place a
/// declaration and rewrite every use. It ships in the one shape where none of that is true: the
/// declaration is <b>already there</b>, as the first statement of the block, and the fix moves it
/// into the condition and deletes it. The variable's name, its scope and its number of evaluations
/// are all unchanged — <c>var t = (T)x;</c> runs once on entry to the block and <c>x is T t</c>
/// assigns once at the same instant.
/// <para>
/// ⚠ The cast is required to name the same type as the test, checked by symbol rather than by
/// spelling. <c>if (x is IList) { var l = (IList&lt;int&gt;)x; }</c> shares a prefix and nothing
/// else, and the two conversions can succeed and fail independently.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypePatternAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.TypePatternOverCast);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.TypePatternOverCast);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IfStatement);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var statement = (IfStatementSyntax)context.Node;

        // ⚠ The pattern variable is in scope in the `else` branch and is not definitely assigned
        // there. A name the `else` declares becomes CS0128 and this rule is not the place to prove
        // it does not.
        if (statement.Else is not null) {
            return;
        }

        if (Unwrap(statement.Condition) is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression } test
            || test.Right is not TypeSyntax tested) {
            return;
        }

        if (!RewriteGuards.IsPlainNamePath(test.Left)) {
            return;
        }

        if (statement.Statement is not BlockSyntax { Statements.Count: > 0 } block
            || block.Statements[0] is not LocalDeclarationStatementSyntax {
                UsingKeyword.RawKind: (int)SyntaxKind.None,
                AwaitKeyword.RawKind: (int)SyntaxKind.None,
                Declaration: { Variables.Count: 1 } declaration
            } first
            || first.Modifiers.Count > 0) {
            return;
        }

        var declarator = declaration.Variables[0];
        if (declarator.Initializer?.Value is not CastExpressionSyntax cast
            || !RewriteGuards.Same(cast.Expression, test.Left)) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        var testedType = model.GetTypeInfo(tested, cancellation).Type;
        var castType = model.GetTypeInfo(cast.Type, cancellation).Type;
        if (testedType is null
            || castType is null
            || testedType.TypeKind == TypeKind.Error
            || castType.TypeKind == TypeKind.Error
            || !SymbolEqualityComparer.Default.Equals(testedType, castType)) {
            return;
        }

        // ⚠ A declaration pattern binds the variable to the tested type. `object o = (Widget)x;` is
        // legal and `x is Widget o` gives `o` the type `Widget`, so an explicitly typed declaration
        // is admitted only where the two are the same type — and `var` already is.
        if (!declaration.Type.IsVar) {
            var declared = model.GetTypeInfo(declaration.Type, cancellation).Type;
            if (declared is null || !SymbolEqualityComparer.Default.Equals(declared, testedType)) {
                return;
            }
        }

        // ⚠ A pattern does not compile inside an expression tree (CS8122), and neither does the
        // statement shape this produces.
        if (NullComparison.InsideExpressionTree(model, statement, cancellation)) {
            return;
        }

        // ⚠ C# scopes a pattern variable declared in an `if` condition to the *enclosing block*,
        // not to the `if`. So the declaration moves one scope outwards and has to answer both
        // halves of the collision question: what is in scope here, and what any neighbouring scope
        // of the same member declares.
        var name = declarator.Identifier.ValueText;
        if (RewriteGuards.WouldCollide(model, statement.SpanStart, name, cancellation)
            || RewriteGuards.DeclaredElsewhereInMember(statement, name)) {
            return;
        }

        if (RewriteGuards.ContainsCommentOrDirective(first)
            || RewriteGuards.ContainsCommentOrDirective(first.SyntaxTree, first.FullSpan)) {
            return;
        }

        var replacement = test.Left + " is " + tested + " " + name;
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(statement.SyntaxTree, test.Span),
                FixEdits.Pack(
                    (test.Span, replacement),
                    (RewriteGuards.LineSpanOf(first), string.Empty)
                ),
                "The test and the cast are one pattern: `" + RewriteGuards.Trim(replacement) + "`"
            )
        );
    }

    static ExpressionSyntax Unwrap(ExpressionSyntax expression) {
        while (expression is ParenthesizedExpressionSyntax parenthesized) {
            expression = parenthesized.Expression;
        }

        return expression;
    }
}
