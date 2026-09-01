using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>
///     ⚠ Whether a declaration references nothing outside itself — the proof <c>static</c> is a
///     compile-time assertion of.
/// </summary>
/// <remarks>
///     Deliberately not <c>SemanticModel.AnalyzeDataFlow</c>. The dataflow answer is about variables,
///     and the question here is the compiler's own rule for a <c>static</c> lambda, local function or
///     method: no <c>this</c>, no <c>base</c>, no instance member reached without a receiver, and no
///     local or parameter from an enclosing scope. Two of those four are not variables at all.
///     <para>
///         ⚠ The walk is a whitelist and every unrecognised shape answers "cannot prove". A name the
///         semantic model does not bind answers the same way, which is what makes the rules built on
///         this silent on a file that does not compile rather than confidently wrong about it — the
///         failure mode docs/plan/16 § R3 spends its length on.
///     </para>
/// </remarks>
internal static class CaptureProof {
    /// <summary>Whether every name inside <paramref name="scope" /> is one <c>static</c> would allow.</summary>
    /// <param name="allow">
    ///     Symbols admitted despite being instance members — used by <c>SK4021</c> for the method's
    ///     recursive call to itself, which the fix makes static alongside the declaration.
    /// </param>
    public static bool UsesNothingOutside(
        SemanticModel model,
        SyntaxNode scope,
        CancellationToken cancellation,
        Func<ISymbol, bool>? allow = null
    ) {
        foreach (var node in scope.DescendantNodes()) {
            cancellation.ThrowIfCancellationRequested();
            switch (node) {
                case ThisExpressionSyntax:
                case BaseExpressionSyntax:
                    return false;

                case SimpleNameSyntax name when !IsMemberName(name) && !InsideNameOf(name, scope, model, cancellation):
                    if (!Permitted(model, name, scope, cancellation, allow)) {
                        return false;
                    }

                    break;
            }
        }

        return true;
    }

    /// <summary>
    ///     The right-hand half of <c>a.b</c>, <c>a?.b</c>, <c>N::b</c>, <c>x: 1</c> and <c>X = 1</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Skipping these is not a hole. The receiver decides whether the access captures, and the
    ///     receiver is a sibling node this walk visits on its own: a bare <c>Count</c> is an implicit
    ///     <c>this.Count</c> and is caught, while the <c>Count</c> in <c>other.Count</c> is not a
    ///     capture and its <c>other</c> is what the walk has to judge.
    /// </remarks>
    static bool IsMemberName(SimpleNameSyntax name) =>
        name.Parent switch {
            MemberAccessExpressionSyntax access => access.Name == name,
            MemberBindingExpressionSyntax binding => binding.Name == name,
            QualifiedNameSyntax qualified => qualified.Right == name,
            AliasQualifiedNameSyntax alias => alias.Name == name,
            NameColonSyntax or NameEqualsSyntax => true,
            _ => false
        };

    /// <summary>
    ///     ⚠ <c>nameof</c> captures nothing: it is a string decided at compile time, and a
    ///     <c>static</c> lambda may name an enclosing local or an instance field inside one.
    /// </summary>
    static bool InsideNameOf(SyntaxNode node, SyntaxNode scope, SemanticModel model, CancellationToken cancellation) {
        for (var current = node.Parent; current is not null && current != scope; current = current.Parent) {
            if (current is InvocationExpressionSyntax {
                    Expression: IdentifierNameSyntax { Identifier.Text: "nameof" },
                    ArgumentList.Arguments.Count: 1
                } invocation
                && model.GetSymbolInfo(invocation, cancellation).Symbol is null) {
                return true;
            }
        }

        return false;
    }

    static bool Permitted(
        SemanticModel model,
        SimpleNameSyntax name,
        SyntaxNode scope,
        CancellationToken cancellation,
        Func<ISymbol, bool>? allow
    ) {
        var symbol = model.GetSymbolInfo(name, cancellation).Symbol;
        if (symbol is null) {
            return false;
        }

        if (allow is not null && allow(symbol)) {
            return true;
        }

        return symbol switch {
            INamespaceSymbol or ITypeSymbol or IAliasSymbol or ILabelSymbol or IDiscardSymbol => true,
            ILocalSymbol or IParameterSymbol or IRangeVariableSymbol => DeclaredInside(symbol, scope),
            IMethodSymbol { MethodKind: MethodKind.LocalFunction } => DeclaredInside(symbol, scope),
            IMethodSymbol method => method.IsStatic,
            IFieldSymbol field => field.IsStatic,
            IPropertySymbol property => property.IsStatic,
            IEventSymbol declared => declared.IsStatic,
            _ => false
        };
    }

    /// <summary>
    ///     ⚠ A symbol with no declaring syntax is not proved to be inside. The implicit <c>value</c> of
    ///     a setter is exactly that shape, and a <c>static</c> lambda naming it does not compile.
    /// </summary>
    static bool DeclaredInside(ISymbol symbol, SyntaxNode scope) =>
        symbol.DeclaringSyntaxReferences.Length > 0
        && symbol.DeclaringSyntaxReferences.All(reference =>
            reference.SyntaxTree == scope.SyntaxTree && scope.Span.Contains(reference.Span)
        );
}
