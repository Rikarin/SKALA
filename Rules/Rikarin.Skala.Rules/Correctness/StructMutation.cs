using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     The one question <c>SK2005</c> and <c>SK2191</c> both have to answer: does this struct method
///     actually write its own instance state?
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         The evidence bar is deliberately "a direct write in the body", not "the method is not
///         <c>readonly</c>".
///     </b> Almost no struct in real code marks its members <c>readonly</c>, so
///     treating an unmarked member as mutating would report every property read through every
///     <c>in</c> parameter in the repository — a defensive copy that is real, invisible, and almost
///     always harmless. What is *not* harmless is a write that is discarded, and a write is something
///     the analysis can see rather than assume.
///     <para>
///         ⚠ The two rules stay disjoint by <em>receiver kind</em> and not by filtering each other's
///         output: <c>SK2005</c> reports a <c>readonly</c> field receiver, <c>SK2191</c> reports a
///         readonly-context local or parameter. No receiver is both, so neither rule can be turned on
///         into a duplicate of the other.
///     </para>
/// </remarks>
static class StructMutation {
    /// <summary>
    ///     Whether a call binds to a struct method whose own body directly assigns instance state, so
    ///     that invoking it through a copy loses the write.
    /// </summary>
    /// <remarks>
    ///     Same-file bodies only. A method whose implementation is in metadata or in another file is
    ///     one the analysis has not read, and guessing at it is how a rule starts being "usually
    ///     right".
    /// </remarks>
    public static bool WritesItsOwnInstanceState(
        SemanticModel model,
        IMethodSymbol method,
        SyntaxTree tree,
        CancellationToken cancellation
    ) {
        if (method is not {
                IsReadOnly: false,
                IsStatic: false,
                ReturnsVoid: true,
                ContainingType: { TypeKind: TypeKind.Struct, IsReadOnly: false }
            }
            || method.DeclaringSyntaxReferences.Length != 1
            || method.DeclaringSyntaxReferences[0].SyntaxTree != tree
            || method.GetAttributes()
                .Any(static attribute => attribute.AttributeClass?.ToDisplayString()
                    == "System.Diagnostics.ConditionalAttribute"
                )
            || method.DeclaringSyntaxReferences[0].GetSyntax(cancellation) is not MethodDeclarationSyntax
            declaration) {
            return false;
        }

        return TopLevelExpressions(declaration).Any(expression => {
                var target = model.GetOperation(expression, cancellation) switch {
                    IAssignmentOperation assignment => assignment.Target,
                    IIncrementOrDecrementOperation increment => increment.Target,
                    _ => null
                };

                return target is IFieldReferenceOperation {
                    Field.IsStatic: false,
                    Instance:
                    IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance }
                };
            }
        );
    }

    static IEnumerable<ExpressionSyntax> TopLevelExpressions(MethodDeclarationSyntax declaration) =>
        declaration.Body is { } body
        ? body.Statements.OfType<ExpressionStatementSyntax>().Select(static statement => statement.Expression)
        : declaration.ExpressionBody is { } arrow
            ? new[] { arrow.Expression }
            : Enumerable.Empty<ExpressionSyntax>();
}
