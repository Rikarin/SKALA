using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2184</c> — a call that skips a better overload the derived interface hides.
/// </summary>
/// <remarks>
///     <para>
///         A derived interface that declares any overload of a name takes the whole name: the base
///         interface's overloads are removed from lookup before overload resolution runs, so the call
///         binds to the derived one even where a base one is the better match.
///     </para>
///     <para>
///         ⚠ <b>This was run, not reasoned about.</b> With <c>IParent.M(string)</c>,
///         <c>IChild : IParent</c> declaring <c>M(object)</c>, and one <c>Impl</c> implementing both:
///         <c>c.M("literal")</c> executes <c>IChild.M(object)</c> and <c>p.M("literal")</c> on the same
///         instance executes <c>IParent.M(string)</c>. Same argument, same object, two methods, and no
///         diagnostic of any kind from the compiler or from any <c>CA*</c> rule.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The genuinely ambiguous member is a compiler error and is deliberately not this
///             rule.
///         </b> Probed at the same time: with <c>IBoth : ILeft, IRight</c> both declaring
///         <c>Value</c> and <c>Run()</c>, <c>b.Value</c> is
///         <b>
///             <c>CS0229</c>
///         </b> and <c>b.Run()</c> is
///         <b>
///             <c>CS0121</c>
///         </b> — both errors. Source shaped like that does not build, so a rule
///         reporting it would report code no analyzer ever sees. Only the binding that succeeds and is
///         not the expected one is left.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Applicability and betterness must both hold, and together they keep
///             <c>IDictionary</c> out.
///         </b> <c>IDictionary&lt;K,V&gt;.Add(K,V)</c> hides
///         <c>ICollection&lt;KeyValuePair&lt;K,V&gt;&gt;.Add(KVP)</c> by exactly this mechanism, but
///         the hidden overload takes one argument and the call passes two, so it is not applicable and
///         there is nothing to report.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HiddenBaseInterfaceOverloadAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.HiddenBaseInterfaceOverload);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ContainsDiagnostics
            || invocation.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } access) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol {
                MethodKind: MethodKind.Ordinary
            } bound
            || bound.ContainingType is not { TypeKind: TypeKind.Interface } declaring
            || model.GetTypeInfo(access.Expression, cancellation).Type is not INamedTypeSymbol {
                TypeKind: TypeKind.Interface
            } receiver) {
            return;
        }

        // Only the interfaces the *bound* method's own interface inherits can be hidden by it.
        var hiding = declaring.OriginalDefinition;
        if (!Declares(receiver, hiding)) {
            return;
        }

        if (Arguments(invocation, model, cancellation) is not { } arguments) {
            return;
        }

        if (model.Compilation is not CSharpCompilation compilation) {
            return;
        }

        foreach (var candidate in declaring.AllInterfaces) {
            foreach (var hidden in candidate.GetMembers(bound.Name)) {
                if (hidden is not IMethodSymbol { MethodKind: MethodKind.Ordinary } other
                    || !IsApplicable(other, arguments, compilation)
                    || !IsBetterThan(other, bound, compilation)) {
                    continue;
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        access.Name.GetLocation(),
                        "`"
                        + other.ToDisplayString()
                        + "` is the better match for these arguments and is unreachable through `"
                        + receiver.ToDisplayString()
                        + "`, so the call binds to `"
                        + bound.ToDisplayString()
                        + "` instead"
                    )
                );

                return;
            }
        }
    }

    static bool Declares(INamedTypeSymbol receiver, INamedTypeSymbol declaring) {
        if (SymbolEqualityComparer.Default.Equals(receiver.OriginalDefinition, declaring)) {
            return true;
        }

        foreach (var inherited in receiver.AllInterfaces) {
            if (SymbolEqualityComparer.Default.Equals(inherited.OriginalDefinition, declaring)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     The static type of every argument, or <c>null</c> where the call is not a plain positional
    ///     one this rule can reason about.
    /// </summary>
    /// <remarks>
    ///     ⚠ Named arguments, <c>ref</c>/<c>out</c>/<c>in</c> modifiers, an argument with no type, and
    ///     anything the analyzer cannot classify are all declines rather than guesses. The whole rule
    ///     rests on being able to say the hidden overload <em>would</em> have taken this call.
    /// </remarks>
    static List<ITypeSymbol>? Arguments(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellation
    ) {
        var result = new List<ITypeSymbol>();
        foreach (var argument in invocation.ArgumentList.Arguments) {
            if (argument.NameColon is not null
                || !argument.RefKindKeyword.IsKind(SyntaxKind.None)
                || model.GetTypeInfo(argument.Expression, cancellation).Type is not { } type
                || !IsUsable(type)) {
                return null;
            }

            result.Add(type);
        }

        return result;
    }

    /// <summary>Whether every argument converts implicitly to the corresponding parameter.</summary>
    static bool IsApplicable(IMethodSymbol method, List<ITypeSymbol> arguments, CSharpCompilation compilation) {
        if (method.Parameters.Length != arguments.Count || method.IsGenericMethod) {
            return false;
        }

        for (var i = 0; i < arguments.Count; i++) {
            var parameter = method.Parameters[i];
            if (parameter.RefKind != RefKind.None
                || !IsUsable(parameter.Type)
                || !compilation.ClassifyConversion(arguments[i], parameter.Type).IsImplicit) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Whether <paramref name="hidden" /> is the strictly more specific of the two signatures.
    /// </summary>
    /// <remarks>
    ///     ⚠ Betterness is decided by conversion and not by heuristic. Every parameter of the hidden
    ///     overload must convert implicitly to the corresponding parameter of the bound one, with at
    ///     least one of those conversions not an identity — which is what <c>M(string)</c> against
    ///     <c>M(object)</c> satisfies and what <c>M(object)</c> against <c>M(int)</c> does not.
    ///     <para>
    ///         ⚠ An identical signature is not reported: that is <c>CS0108</c>, and the compiler is
    ///         already asking for <c>new</c> there.
    ///     </para>
    /// </remarks>
    static bool IsBetterThan(IMethodSymbol hidden, IMethodSymbol bound, CSharpCompilation compilation) {
        if (hidden.Parameters.Length != bound.Parameters.Length
            || SymbolEqualityComparer.Default.Equals(hidden, bound)) {
            return false;
        }

        var narrower = false;
        for (var i = 0; i < hidden.Parameters.Length; i++) {
            var from = hidden.Parameters[i].Type;
            var to = bound.Parameters[i].Type;
            if (!IsUsable(from) || !IsUsable(to)) {
                return false;
            }

            var conversion = compilation.ClassifyConversion(from, to);
            if (conversion.IsIdentity) {
                continue;
            }

            if (!conversion.IsImplicit) {
                return false;
            }

            narrower = true;
        }

        return narrower;
    }

    static bool IsUsable(ITypeSymbol type) =>
        type.TypeKind is not (TypeKind.Error or TypeKind.Dynamic or TypeKind.TypeParameter or TypeKind.Unknown);
}
