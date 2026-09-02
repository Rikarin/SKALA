using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2112</c> — a local declared <c>T?</c> that the compiler proves is never null.
/// </summary>
/// <remarks>
///     The reverse direction of the nullable migration. A <c>?</c> the flow analysis proves unnecessary
///     is a null check every subsequent line has to justify for nothing, and it teaches the reader to
///     stop believing the annotations — which is the whole value of having them.
///     <para>
///         ⚠
///         <b>
///             <c>ReturnTypeCanBeNotNullable</c>, the other half of ReSharper's concept, is cut and the
///             reason is a defect rather than noise.
///         </b> Narrowing a <em>method's</em> return annotation
///         propagates to every call site through <c>var</c>: <c>var x = M();</c> infers <c>string?</c>
///         today and <c>string</c> afterwards, so a later <c>x = null</c> becomes a new warning in a file
///         this analyzer never saw. A <see cref="DiagnosticAnalyzer" /> is handed one syntax tree and
///         cannot enumerate callers. ReSharper answers it from a solution-wide index; Skala has none at
///         analysis time, so the rule stops at the local — whose annotation nothing outside the method
///         can observe — and the same cascade <em>inside</em> one method is guarded directly.
///     </para>
///     <para>
///         ⚠ <b>Reference types only.</b> <c>int? i = 1;</c> is a different edit entirely: removing that
///         <c>?</c> changes the type rather than an annotation and breaks <c>HasValue</c>, boxing and
///         comparison against null.
///     </para>
///     <para>
///         ⚠ <b>The rule withdraws where nullable annotations are off at the declaration</b>, and it
///         withdraws twice over — the explicit context check, and the flow state, which is
///         <see cref="NullableFlowState.None" /> there rather than <c>NotNull</c>. In that context the
///         <c>?</c> is already <c>CS8632</c> and removing it is the compiler's finding, not this one.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullableLocalNeverNullAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NullableLocalNeverNull);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LocalDeclarationStatement);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var statement = (LocalDeclarationStatementSyntax)context.Node;
        if (statement.Declaration.Type is not NullableTypeSyntax nullable) {
            return;
        }

        if (statement.UsingKeyword.RawKind != (int)SyntaxKind.None || statement.Modifiers.Count > 0) {
            // `const string? x` does not compile and `using` bindings hand the value to a disposer;
            // neither is worth an edit, and both are cheaper to exclude than to reason about.
            return;
        }

        if (!NullabilityFacts.AnnotationsEnabledAt(context.SemanticModel, statement.SpanStart)) {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(nullable.ElementType, context.CancellationToken).Type is not {
                IsReferenceType: true
            }) {
            return;
        }

        var scope = RewriteGuards.ScopeRoot(statement);
        var locals = new List<ILocalSymbol>();
        foreach (var declarator in statement.Declaration.Variables) {
            // ⚠ The `?` belongs to the shared type, so one declarator that could still be null keeps it
            // for all of them. A rule that reported the declaration because its *first* declarator was
            // provably non-null would write a fix that breaks the second.
            if (declarator.Initializer is not { } initializer
                || !NullabilityFacts.IsProvenNotNull(
                    context.SemanticModel,
                    initializer.Value,
                    context.CancellationToken
                )) {
                return;
            }

            if (context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken) is not
                ILocalSymbol local) {
                return;
            }

            locals.Add(local);
        }

        if (locals.Count == 0) {
            return;
        }

        foreach (var local in locals) {
            if (IsWrittenOrEscapes(context.SemanticModel, scope, statement, local, context.CancellationToken)) {
                return;
            }
        }

        if (RewriteGuards.ContainsCommentOrDirective(nullable)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                nullable.QuestionToken.GetLocation(),
                FixEdits.Pack((nullable.QuestionToken.Span, string.Empty)),
                "`"
                + RewriteGuards.Trim(locals[0].Name)
                + "` is declared `"
                + nullable
                + "` and is only ever assigned a value that is not null"
            )
        );
    }

    /// <summary>
    ///     Whether anything in the member could give the local a different value, or could observe its
    ///     declared annotation.
    /// </summary>
    /// <remarks>
    ///     ⚠ The walk covers the <em>whole</em> member including lambdas and local functions, which is
    ///     the opposite of what <c>SK2110</c> wants from the same tree: an assignment written inside a
    ///     lambda still assigns this method's local, and stopping at the lambda would miss exactly the
    ///     case a reader cannot see either.
    ///     <para>
    ///         ⚠ <b>The <c>var</c> guard is the cascade guard and it over-bails on purpose.</b> Removing
    ///         the <c>?</c> changes what <c>var t = s;</c> infers, so a later <c>t = null</c> becomes a
    ///         new warning that the fix caused. Any mention of the local anywhere inside an
    ///         implicitly-typed declaration's initialiser is declined rather than only the direct
    ///         assignment, because the inference travels through expressions this rule does not model.
    ///     </para>
    ///     <para>
    ///         ⚠ <c>ref</c> and <c>out</c> arguments are declined: the callee decides the value, and the
    ///         parameter's own annotation is what the call site has to match.
    ///     </para>
    /// </remarks>
    static bool IsWrittenOrEscapes(
        SemanticModel model,
        SyntaxNode scope,
        LocalDeclarationStatementSyntax declaration,
        ILocalSymbol local,
        CancellationToken token
    ) {
        foreach (var node in scope.DescendantNodes()) {
            token.ThrowIfCancellationRequested();
            switch (node) {
                case AssignmentExpressionSyntax assignment when Refers(model, assignment.Left, local, token):
                    return true;

                case ArgumentSyntax { RefKindKeyword.RawKind: not (int)SyntaxKind.None } argument
                    when Refers(model, argument.Expression, local, token):
                    return true;

                case RefExpressionSyntax reference when Refers(model, reference.Expression, local, token):
                    return true;

                case VariableDeclarationSyntax { Type: IdentifierNameSyntax { IsVar: true } } inferred
                    when !inferred.Span.OverlapsWith(declaration.Span) && Mentions(model, inferred, local, token):
                    return true;

                case ForEachStatementSyntax { Type: IdentifierNameSyntax { IsVar: true } } loop
                    when Refers(model, loop.Expression, local, token):
                    return true;
            }
        }

        return false;
    }

    static bool Mentions(
        SemanticModel model,
        VariableDeclarationSyntax declaration,
        ILocalSymbol local,
        CancellationToken token
    ) {
        foreach (var declarator in declaration.Variables) {
            if (declarator.Initializer is not { } initializer) {
                continue;
            }

            foreach (var node in initializer.DescendantNodesAndSelf()) {
                if (node is IdentifierNameSyntax identifier && Refers(model, identifier, local, token)) {
                    return true;
                }
            }
        }

        return false;
    }

    static bool Refers(SemanticModel model, ExpressionSyntax expression, ILocalSymbol local, CancellationToken token) {
        while (expression is ParenthesizedExpressionSyntax parenthesized) {
            expression = parenthesized.Expression;
        }

        return expression is IdentifierNameSyntax
            && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(expression, token).Symbol, local);
    }
}
