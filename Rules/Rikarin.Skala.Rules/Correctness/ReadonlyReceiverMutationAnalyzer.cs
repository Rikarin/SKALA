using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2191</c> — a mutating struct method is called through a receiver the language will not
///     let it write, so the call operates on a copy that is thrown away.
/// </summary>
/// <remarks>
///     ⚠ <b>This is <c>SK2005</c>'s defect at the receivers <c>SK2005</c> does not have.</b>
///     <c>SK2005</c> reports a <c>readonly</c> <em>field</em> receiver and nothing else — its own
///     <c>negative/parameter.cs</c> asserts silence on an <c>in</c> parameter. The three receivers
///     here are the rest of the same shape: an <c>in</c> (or <c>ref readonly</c>) parameter, a
///     <c>ref readonly</c> local, and a <c>foreach</c> iteration variable. Each is a value the callee
///     may read and may not write, so the compiler inserts a defensive copy, the method mutates the
///     copy, and the write is discarded without a word from anybody.
///     <para>
///         ⚠ <b>The <c>foreach</c> case does not need a defensive copy to be the same bug.</b> The
///         iteration variable is already a copy of the element, so the mutation is lost for a
///         different mechanical reason and to exactly the same effect. What is excluded is
///         <c>foreach (ref var x in span)</c>, where the variable aliases the element and the write
///         lands.
///     </para>
///     <para>
///         ⚠ The bar for "mutating" is a write the analysis has read in the method's own body, not
///         the absence of a <c>readonly</c> modifier — see <see cref="StructMutation" /> for why
///         the looser test would report most of the repository.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadonlyReceiverMutationAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ReadonlyReceiverMutation);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (context.Node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax }
            || model.GetOperation(context.Node, cancellation) is not IInvocationOperation {
                Instance: { } receiver,
                TargetMethod: { } method
            }
            || Describe(receiver) is not { } description
            || !StructMutation.WritesItsOwnInstanceState(model, method, context.Node.SyntaxTree, cancellation)
            || NullComparison.InsideExpressionTree(model, context.Node, cancellation)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                context.Node.GetLocation(),
                "`" + method.Name + "` writes struct state through " + description + ", so the write is discarded"
            )
        );
    }

    /// <summary>
    ///     The receiver kinds that cannot carry a write back, each named the way its declaration is
    ///     spelled — or <c>null</c> for every receiver that can.
    /// </summary>
    static string? Describe(IOperation receiver) =>
        receiver switch {
            IParameterReferenceOperation { Parameter: { RefKind: RefKind.In } parameter } =>
                "the `in` parameter `" + parameter.Name + "`",
            IParameterReferenceOperation {
                Parameter: { RefKind: RefKind.RefReadOnlyParameter } parameter
            } => "the `ref readonly` parameter `" + parameter.Name + "`",
            ILocalReferenceOperation { Local: { RefKind: RefKind.RefReadOnly } local } =>
                "the `ref readonly` local `" + local.Name + "`",
            ILocalReferenceOperation { Local: { RefKind: RefKind.None } local } when IsByValueForeachVariable(local) =>
                "the `foreach` variable `" + local.Name + "`, which is a copy of the element",
            _ => null
        };

    /// <summary>
    ///     ⚠ <c>foreach (ref var x in span)</c> is excluded here and not by a later filter: the ref
    ///     form aliases the element, so the write it performs is the one the author asked for.
    /// </summary>
    static bool IsByValueForeachVariable(ILocalSymbol local) =>
        local.DeclaringSyntaxReferences.Length == 1
        && local.DeclaringSyntaxReferences[0].GetSyntax() is ForEachStatementSyntax { Type: not RefTypeSyntax };
}
