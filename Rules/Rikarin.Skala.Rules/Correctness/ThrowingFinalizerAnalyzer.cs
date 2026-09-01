using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2090</c> — a <c>throw</c> that can escape a finalizer.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK2000 — Correctness". An exception that leaves a finalizer is
///     not caught anywhere: the runtime tears the process down from the finalizer thread, and the stack
///     it prints names the finalizer queue rather than the code that put the object on it. There is no
///     <c>try</c> a caller can write that helps, because there is no caller.
///     <para>
///         ⚠ <b>The finalizer body is almost never where the throw is.</b> The documented disposal
///         pattern makes <c>~T()</c> a one-line <c>Dispose(false)</c>, so a rule that reads only the
///         destructor's own block would be silent on nearly every real occurrence. This rule therefore
///         follows <b>exactly one</b> call hop, into a method declared on the same type whose body is in
///         this compilation. The recall that costs is named rather than hidden: a
///         <c>Dispose(bool)</c> inherited from another assembly is not followed, a
///         <c>Dispose(false)</c> that throws two calls down is not followed, and a <c>virtual</c>
///         member overridden by a derived type is read as the body the finalized type itself runs.
///     </para>
///     <para>
///         ⚠ <b>The <c>disposing</c> guard is what keeps the hop honest.</b> The pattern's whole point is
///         that <c>if (disposing) { … }</c> holds the managed cleanup, which the finalizer path never
///         enters — so a <c>throw</c> inside that branch is unreachable from <c>~T()</c> and reporting it
///         would fire on every correct implementation of the pattern. When the finalizer passes the
///         literal <c>false</c>, throws that the branch condition proves unreachable are dropped.
///     </para>
///     <para>
///         Report-only. Wrapping the body in <c>try { … } catch { }</c> silences the rule and swallows
///         the failure, and deleting the <c>throw</c> deletes whatever the author meant by it. Which of
///         the two is right is a decision about what the type owns.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ThrowingFinalizerAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.FinalizerCanThrow);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.DestructorDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var finalizer = (DestructorDeclarationSyntax)context.Node;
        SyntaxNode? body = finalizer.Body ?? (SyntaxNode?)finalizer.ExpressionBody?.Expression;
        if (body is null) {
            return;
        }

        foreach (var thrown in Escaping(body, null)) {
            Report(context, thrown, "a `throw` that leaves a finalizer terminates the process");
        }

        if (context.SemanticModel.GetDeclaredSymbol(finalizer, context.CancellationToken)
            is not { ContainingType: { IsAbstract: false } owner }) {
            return;
        }

        foreach (var invocation in body.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()) {
            // A call inside a `try` with a `catch` cannot put anything on the finalizer's exit path,
            // so neither can anything it reaches.
            if (!ExceptionFlow.CanEscape(invocation, body)) {
                continue;
            }

            if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
                is not IMethodSymbol callee
                || !SymbolEqualityComparer.Default.Equals(callee.ContainingType, owner)
                || callee.MethodKind != MethodKind.Ordinary) {
                continue;
            }

            if (Declaration(callee) is not { } declared) {
                continue;
            }

            SyntaxNode? calleeBody = declared.Body ?? (SyntaxNode?)declared.ExpressionBody?.Expression;
            if (calleeBody is null) {
                continue;
            }

            // ⚠ The one piece of value tracking the rule does, and the reason it does not fire on the
            // documented pattern: `Dispose(false)` makes every `if (disposing)` branch dead.
            var falseParameter = FalseBooleanParameter(context, invocation, callee, declared);

            foreach (var thrown in Escaping(calleeBody, falseParameter)) {
                Report(
                    context,
                    thrown,
                    "`" + callee.Name + "` is reached from the finalizer and can throw from there, which "
                    + "terminates the process"
                );
            }
        }
    }

    static void Report(SyntaxNodeAnalysisContext context, SyntaxNode thrown, string message) =>
        context.ReportDiagnostic(Diagnostic.Create(Descriptor, thrown.GetLocation(), message));

    /// <summary>The <c>throw</c>s in <paramref name="body" /> that reach its exit.</summary>
    static IEnumerable<SyntaxNode> Escaping(SyntaxNode body, IParameterSymbol? knownFalse) {
        foreach (var node in ExceptionFlow.Throws(body)) {
            if (ExceptionFlow.CanEscape(node, body) && !Unreachable(node, body, knownFalse)) {
                yield return node;
            }
        }
    }

    /// <summary>
    ///     Whether a branch condition on a parameter known to be <c>false</c> proves the node dead.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the guard that decides whether the rule is usable. `if (disposing) { … }` is the
    ///     disposal pattern's entire structure, the finalizer passes `false`, and everything inside that
    ///     branch is unreachable from `~T()`. Without this, every correct implementation of the pattern
    ///     that throws anywhere in its managed half is a false positive.
    /// </remarks>
    static bool Unreachable(SyntaxNode node, SyntaxNode body, IParameterSymbol? knownFalse) {
        if (knownFalse is null) {
            return false;
        }

        for (var current = node; current is not null && current != body; current = current.Parent) {
            // ⚠ The `else` arm hangs off an `ElseClauseSyntax`, not off the `if` — so the walk meets
            // the clause, and `branch.Else.Statement` is the block one level below it. Comparing the
            // clause against that block is the mistake that makes this guard silently never fire on
            // `if (!disposing) { … } else { throw … }`, which is the half of the idiom that matters.
            var (branch, required) = current switch {
                _ when current.Parent is IfStatementSyntax outer && outer.Statement == current => (outer, true),
                ElseClauseSyntax { Parent: IfStatementSyntax outer } => (outer, false),
                _ => (null, false)
            };

            if (branch is not null && Tests(branch.Condition, knownFalse) is { } positive && positive == required) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     <c>disposing</c> as <c>true</c>, <c>!disposing</c> as <c>false</c>, anything else as unknown.
    /// </summary>
    static bool? Tests(ExpressionSyntax condition, IParameterSymbol parameter) =>
        condition switch {
            ParenthesizedExpressionSyntax parenthesized => Tests(parenthesized.Expression, parameter),
            PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } negated =>
                Tests(negated.Operand, parameter) is { } inner ? !inner : null,
            IdentifierNameSyntax name when string.Equals(
                name.Identifier.ValueText,
                parameter.Name,
                System.StringComparison.Ordinal
            ) => true,
            _ => null
        };

    /// <summary>
    ///     The callee's <c>bool</c> parameter that this call site passes the literal <c>false</c>.
    /// </summary>
    static IParameterSymbol? FalseBooleanParameter(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IMethodSymbol callee,
        MethodDeclarationSyntax declared
    ) {
        if (invocation.ArgumentList is null) {
            return null;
        }

        var arguments = invocation.ArgumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++) {
            if (!arguments[i].Expression.IsKind(SyntaxKind.FalseLiteralExpression)) {
                continue;
            }

            var parameter = arguments[i].NameColon is { } named
                ? callee.Parameters.FirstOrDefault(p =>
                    string.Equals(p.Name, named.Name.Identifier.ValueText, System.StringComparison.Ordinal)
                )
                : i < callee.Parameters.Length
                    ? callee.Parameters[i]
                    : null;

            // The parameter symbol has to be the one the *declaration* in hand binds, because that is
            // the syntax `Unreachable` walks. Matching by name is what makes the two line up.
            if (parameter is { Type.SpecialType: SpecialType.System_Boolean }
                && declared.ParameterList.Parameters.Any(p =>
                    string.Equals(p.Identifier.ValueText, parameter.Name, System.StringComparison.Ordinal)
                )) {
                return parameter;
            }
        }

        return null;
    }

    static MethodDeclarationSyntax? Declaration(IMethodSymbol method) {
        foreach (var reference in method.DeclaringSyntaxReferences) {
            if (reference.GetSyntax() is MethodDeclarationSyntax declaration) {
                return declaration;
            }
        }

        return null;
    }
}
