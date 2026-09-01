using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>
///     <c>SK4020</c> — a lambda, anonymous method or local function that captures nothing and does not
///     say so.
/// </summary>
/// <remarks>
///     ⚠ Disjoint from <c>SK4002</c> by construction rather than by a filter. That rule reports a
///     delegate capturing iteration-local storage; this one reports a declaration that captures
///     nothing at all. No declaration satisfies both, so the two never double-report the same lambda
///     and neither had to be taught about the other.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoncapturingLambdaAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.NoncapturingLambda);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NoncapturingLambda);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    Analyze,
                    SyntaxKind.SimpleLambdaExpression,
                    SyntaxKind.ParenthesizedLambdaExpression,
                    SyntaxKind.AnonymousMethodExpression,
                    SyntaxKind.LocalFunctionStatement
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var node = context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (Modifiers(node).Any(SyntaxKind.StaticKeyword)) {
            return;
        }

        if (node is AnonymousFunctionExpressionSyntax function
            && (!IsDelegateConversion(model, function, cancellation) || InsideExpressionTree(model, node, cancellation))) {
            return;
        }

        if (node is LocalFunctionStatementSyntax { Body: null, ExpressionBody: null }) {
            return;
        }

        if (!CaptureProof.UsesNothingOutside(model, node, cancellation)) {
            return;
        }

        var subject = node is LocalFunctionStatementSyntax ? "Local function" : "Lambda";
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Header(node),
                FixEdits.Pack((new TextSpan(Insertion(node), 0), "static ")),
                subject + " captures nothing; mark it `static` so it cannot allocate a closure"
            )
        );
    }

    /// <summary>
    ///     ⚠ An expression tree is not a delegate, and a lambda converted to one is describing itself
    ///     to a provider rather than compiling to a method.
    /// </summary>
    static bool IsDelegateConversion(
        SemanticModel model,
        AnonymousFunctionExpressionSyntax function,
        System.Threading.CancellationToken cancellation
    ) =>
        model.GetTypeInfo(function, cancellation).ConvertedType?.TypeKind == TypeKind.Delegate;

    /// <summary>A delegate lambda nested inside an expression-tree lambda is left alone as well.</summary>
    static bool InsideExpressionTree(
        SemanticModel model,
        SyntaxNode node,
        System.Threading.CancellationToken cancellation
    ) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case AnonymousFunctionExpressionSyntax outer when !IsDelegateConversion(model, outer, cancellation):
                    return true;

                case MemberDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }

    static SyntaxTokenList Modifiers(SyntaxNode node) =>
        node switch {
            SimpleLambdaExpressionSyntax lambda => lambda.Modifiers,
            ParenthesizedLambdaExpressionSyntax lambda => lambda.Modifiers,
            AnonymousMethodExpressionSyntax method => method.Modifiers,
            LocalFunctionStatementSyntax function => function.Modifiers,
            _ => default
        };

    /// <summary>
    ///     ⚠ Before the modifiers, not after: <c>static async</c> is the order the language spells and
    ///     the arranger expects, and inserting after <c>async</c> would produce text a later formatting
    ///     pass has to move.
    /// </summary>
    static int Insertion(SyntaxNode node) {
        var modifiers = Modifiers(node);
        if (modifiers.Count > 0) {
            return modifiers[0].SpanStart;
        }

        return node switch {
            SimpleLambdaExpressionSyntax lambda => lambda.Parameter.SpanStart,
            ParenthesizedLambdaExpressionSyntax lambda =>
                (lambda.ReturnType ?? (SyntaxNode)lambda.ParameterList).SpanStart,
            AnonymousMethodExpressionSyntax method => method.DelegateKeyword.SpanStart,
            LocalFunctionStatementSyntax function => function.ReturnType.SpanStart,
            _ => node.SpanStart
        };
    }

    /// <summary>The declaration's header, so a hundred-line body is not the reported span.</summary>
    static Location Header(SyntaxNode node) =>
        node switch {
            SimpleLambdaExpressionSyntax lambda => lambda.Parameter.GetLocation(),
            ParenthesizedLambdaExpressionSyntax lambda => lambda.ParameterList.GetLocation(),
            AnonymousMethodExpressionSyntax method => method.DelegateKeyword.GetLocation(),
            LocalFunctionStatementSyntax function => function.Identifier.GetLocation(),
            _ => node.GetLocation()
        };
}
