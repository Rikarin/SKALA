using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>SK1026: constant ASCII encoded only to feed an already-selected read-only byte-span parameter.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Utf8LiteralAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.Utf8Literal);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, "11.0")) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "GetBytes" }
            || invocation.ArgumentList.Arguments.Count != 1
            || invocation.Parent is not ArgumentSyntax argument
            || argument.Parent?.Parent is not InvocationExpressionSyntax consumer
            || invocation.ContainsDirectives
            || RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(invocation.SyntaxTree, invocation.Span)) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (Utf8EncodingCall.Bind(model, invocation, cancellation) is not { } call
            || call.TargetMethod.Parameters[0].Type.SpecialType != SpecialType.System_String
            || model.GetConstantValue(invocation.ArgumentList.Arguments[0].Expression, cancellation) is not {
                HasValue: true,
                Value: string text
            }
            || text.Any(static character => character > 127)
            || !ConstantDependencies.AreFileLocal(model, invocation.ArgumentList.Arguments[0].Expression, cancellation)
            || model.GetOperation(argument, cancellation) is not IArgumentOperation {
                Parameter.Type: INamedTypeSymbol parameterType
            }
            || parameterType.TypeArguments.Length != 1
            || parameterType.TypeArguments[0].SpecialType != SpecialType.System_Byte
            || !SymbolEqualityComparer.Default.Equals(
                parameterType.OriginalDefinition,
                model.Compilation.GetTypeByMetadataName("System.ReadOnlySpan`1")
            )
            || model.GetSymbolInfo(consumer, cancellation).Symbol is not IMethodSymbol originalMethod
            || NullComparison.InsideExpressionTree(model, consumer, cancellation)
            || CallerArgumentSafety.CapturesText(model, invocation, cancellation)) {
            return;
        }

        var literal = SyntaxFactory.ParseExpression(SyntaxFactory.Literal(text).Text + "u8");
        var replacement = consumer.ReplaceNode(invocation, literal);
        if (!SymbolEqualityComparer.Default.Equals(
                originalMethod,
                model.GetSpeculativeSymbolInfo(
                    consumer.SpanStart,
                    replacement,
                    SpeculativeBindingOption.BindAsExpression
                ).Symbol
            )) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                FixEdits.Pack((invocation.Span, literal.ToString())),
                "Use a UTF-8 literal instead of allocating an encoded ASCII byte array"
            )
        );
    }
}
