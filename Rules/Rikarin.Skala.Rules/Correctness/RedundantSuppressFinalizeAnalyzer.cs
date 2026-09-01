using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2032</c> — <c>GC.SuppressFinalize(this)</c> where no finalizer exists to suppress.
/// </summary>
/// <remarks>
///     Copied out of the canonical <c>Dispose</c> pattern into types that never had a finalizer. It
///     costs nothing at runtime and it reads as though the type has one, which sends the next reader
///     looking for something that is not there.
///     <para>
///         ⚠ <b><c>sealed</c>, and directly derived from <c>object</c>, are the whole rule.</b> In an
///         unsealed type the call is not redundant at all: a derived type may declare a finalizer, and
///         the base's <c>Dispose</c> is then the thing that suppresses it. A base class other than
///         <c>object</c> is the same argument one level up. Dropping either condition turns a tidy
///         deletion into a resurrection of the finalizer queue, which is the failure this rule must
///         never cause.
///     </para>
///     <para>
///         ⚠ <b>Single, non-partial declaration.</b> The rule declares <c>scope: Semantic</c>, which
///         promises the incremental cache that its answer for a file depends only on that file. A
///         partial type's finalizer can be in another one, so a type with more than one declaring
///         reference is skipped rather than answered from a symbol the cache key does not name.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantSuppressFinalizeAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RedundantSuppressFinalize);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // ⚠ The fix is a deletion, so the finding is withheld wherever there is nothing to delete.
        // An expression-bodied `Dispose() => GC.SuppressFinalize(this);` has no statement, and an
        // embedded `if (x) GC.SuppressFinalize(this);` leaves an `if` with no body — a fix that does
        // not compile is worse than no fix (docs/plan/10), and reporting without one would break the
        // catalogue's `hasFix` promise.
        if (invocation.Parent is not ExpressionStatementSyntax { Parent: BlockSyntax } statement) {
            return;
        }

        if (invocation.ArgumentList.Arguments.Count != 1
            || invocation.ArgumentList.Arguments[0] is not {
                RefOrOutKeyword.RawKind: (int)SyntaxKind.None,
                Expression: ThisExpressionSyntax
            }) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol {
                IsStatic: true,
                Name: "SuppressFinalize"
            } method
            || !SymbolEqualityComparer.Default.Equals(
                method.ContainingType,
                context.Compilation.GetTypeByMetadataName("System.GC")
            )) {
            return;
        }

        if (model.GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression, cancellation).Type
            is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsSealed: true, IsStatic: false } type
            || type.BaseType?.SpecialType != SpecialType.System_Object
            || type.DeclaringSyntaxReferences.Length != 1
            || HasFinalizer(type)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                FixEdits.Pack((Deletion(statement), string.Empty)),
                "`GC.SuppressFinalize` has nothing to suppress: `"
                + type.Name
                + "` is sealed, declares no finalizer and inherits none"
            )
        );
    }

    static bool HasFinalizer(INamedTypeSymbol type) {
        foreach (var member in type.GetMembers()) {
            if (member is IMethodSymbol { MethodKind: MethodKind.Destructor }) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     The span to remove: the statement, and the line break and indentation in front of it.
    /// </summary>
    /// <remarks>
    ///     ⚠ Unless something is written in that leading trivia. A comment there is usually about the
    ///     statement and could go with it, but it can equally be a comment about the rest of the block,
    ///     and deleting somebody's sentence is not a change a <c>fixIsSafe</c> rule may make without
    ///     review. Where there is one, only the statement goes and the formatter tidies the blank line.
    /// </remarks>
    static TextSpan Deletion(StatementSyntax statement) {
        foreach (var trivia in statement.GetLeadingTrivia()) {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia)) {
                return statement.Span;
            }
        }

        var previous = statement.GetFirstToken().GetPreviousToken();
        return previous.Span.End <= 0 || previous.Span.End > statement.SpanStart
            ? statement.Span
            : TextSpan.FromBounds(previous.Span.End, statement.Span.End);
    }
}
