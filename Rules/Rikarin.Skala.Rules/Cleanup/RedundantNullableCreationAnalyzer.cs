using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Cleanup;

/// <summary>
///     <c>SK0290</c> — <c>new int?(5)</c> where the position already imposes <c>int?</c>.
/// </summary>
/// <remarks>
///     <para>
///         The wrapper converts nothing: <c>int? x = new int?(5);</c> and <c>int? x = 5;</c> declare the
///         same variable, because the implicit conversion from <c>T</c> to <c>T?</c> is the constructor
///         call written out. Both spellings are the same symbol —
///         <c>new int?(5)</c> and <c>new Nullable&lt;int&gt;(5)</c> — so both are covered.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Whether the wrapper converts nothing is a question about the position, not about the
///             expression
///         </b>, and the semantic model cannot be asked it.
///         <c>GetSpeculativeTypeInfo</c> at a position binds the operand as a <em>standalone</em>
///         expression, so for <c>new int?(5)</c> the operand's <c>ConvertedType</c> comes back
///         <c>int</c> — in every context, including the ones where the rewrite is correct. A guard
///         built on it withdraws every finding and looks exactly like a rule with nothing to find,
///         which is <c>SK0234</c>'s recorded lesson
///         ([#128](https://github.com/Rikarin/SKALA/issues/128)).
///     </para>
///     <para>
///         So the target type is not inferred, it is <b>read off the syntax</b>: four positions
///         write it down, and the rule reports in those four and nowhere else. Under <c>var</c> the
///         wrapper is load-bearing — <c>var x = new int?(5);</c> types <c>x</c> as <c>int?</c> and
///         <c>var x = 5;</c> types it as <c>int</c> — which is the same trap <c>SK0234</c> is written
///         around, one construct over.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantNullableCreationAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RedundantNullableCreation);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // ⚠ `ObjectCreationExpression` only. `new(5)` is an `ImplicitObjectCreationExpression`, a
        // different kind and not this shape at all: the type is not written there, it is the target
        // type, and there is nothing redundant about naming it implicitly.
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ObjectCreationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        // ⚠ `new int?()` has no operand to leave behind — the deletion would produce nothing, not a
        // shorter expression. An initializer is refused for the same reason the fix is two deletions:
        // the tail span would carry the braces away with it.
        if (creation.Initializer is not null
            || creation.ArgumentList is not { Arguments.Count: 1 } list
            || list.Arguments[0] is not { NameColon: null } argument) {
            return;
        }

        if (model.GetTypeInfo(creation.Type, cancellation).Type is not INamedTypeSymbol {
                ConstructedFrom.SpecialType: SpecialType.System_Nullable_T,
                TypeArguments.Length: 1
            } nullable) {
            return;
        }

        var underlying = nullable.TypeArguments[0];
        var operand = argument.Expression;

        // ⚠ **Exactly `T`, and this deliberately declines a widening operand.** `new int?(someByte)`
        // is doing a numeric conversion inside the constructor call, and `int? x = someByte;` does the
        // same one — but the two are the same only for the conversions that are implicit, and asking
        // for the identical type is how that question is not asked at all.
        if (underlying.TypeKind == TypeKind.Error
            || model.GetTypeInfo(operand, cancellation).Type is not { } source
            || source.TypeKind == TypeKind.Error
            || !SymbolEqualityComparer.Default.Equals(source, underlying)) {
            return;
        }

        if (!PositionWritesTheType(model, creation, operand, nullable, cancellation)) {
            return;
        }

        // Two deletions: `new int?(` and the `)`. ⚠ Every whitelisted position accepts an arbitrary
        // expression without parentheses — an initializer, a `return` operand, an assignment's right
        // side and an argument each end at a token the operand cannot contain unbracketed — so the fix
        // never has to add any.
        var head = TextSpan.FromBounds(creation.SpanStart, operand.SpanStart);
        var tail = TextSpan.FromBounds(operand.Span.End, creation.Span.End);
        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(creation.SyntaxTree, head)
            || RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(creation.SyntaxTree, tail)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                creation.GetLocation(),
                FixEdits.Pack((head, string.Empty), (tail, string.Empty)),
                "The position already imposes `"
                + nullable.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                + "`, so the explicit construction converts nothing"
            )
        );
    }

    /// <summary>
    ///     The whitelist: the four positions that <em>write</em> the target type down.
    /// </summary>
    /// <remarks>
    ///     ⚠ A whitelist rather than a query, because the query does not exist — see the type remarks.
    ///     Everything not named here is declined, including every position whose target type comes
    ///     from inference (<c>var</c>, a collection or object initializer element, a conditional's
    ///     other arm, a lambda's inferred delegate) and every position where deleting the wrapper can
    ///     move overload resolution.
    /// </remarks>
    static bool PositionWritesTheType(
        SemanticModel model,
        ObjectCreationExpressionSyntax creation,
        ExpressionSyntax operand,
        INamedTypeSymbol nullable,
        CancellationToken cancellation
    ) =>
        creation.Parent switch {
            // (1) A declaration initializer — a local or a field. ⚠ Not under `var`: `var x = new
            // int?(5);` types `x` as `int?` and `var x = 5;` types it as `int`, and `GetTypeInfo` on
            // the `var` keyword answers `int?` for both, so only the syntax separates them.
            EqualsValueClauseSyntax {
                Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration }
            } => !declaration.Type.IsVar && IsWritten(model, declaration.Type, nullable, cancellation),

            // (2) A return position, under a member whose return type is written.
            ReturnStatementSyntax statement when statement.Expression == creation =>
                IsWritten(model, WrittenReturnType(statement), nullable, cancellation),
            ArrowExpressionClauseSyntax arrow when arrow.Expression == creation =>
                IsWritten(model, WrittenReturnType(arrow), nullable, cancellation),

            // (3) A simple assignment. The left side always has a fixed declared type — even a `var`
            // local's, which was fixed at its declaration — so the assignment cannot widen it.
            AssignmentExpressionSyntax assignment when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                && assignment.Right == creation =>
                model.GetTypeInfo(assignment.Left, cancellation).Type is { TypeKind: not TypeKind.Error } left
                && SymbolEqualityComparer.Default.Equals(left, nullable),

            // (4) An argument, where the parameter's type is the written one — and only where the
            // call still reaches the identical symbol without the wrapper.
            ArgumentSyntax argument when argument.Parent is ArgumentListSyntax {
                Parent: InvocationExpressionSyntax or ObjectCreationExpressionSyntax
            } => ArgumentKeepsItsCall(model, argument, creation, operand, nullable, cancellation),

            _ => false
        };

    /// <summary>Whether <paramref name="type" /> is written and binds to the same nullable type.</summary>
    static bool IsWritten(
        SemanticModel model,
        TypeSyntax? type,
        INamedTypeSymbol nullable,
        CancellationToken cancellation
    ) =>
        type is not null
        && model.GetTypeInfo(type, cancellation).Type is { } declared
        && SymbolEqualityComparer.Default.Equals(declared, nullable);

    /// <summary>
    ///     The written return type of the member a <c>return</c> or an expression body belongs to.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>A lambda and an anonymous method stop the walk and return nothing.</b> Their target
    ///     type comes from the delegate being converted to, not from anything written at the return —
    ///     and without this the walk would climb straight past them into the enclosing method and read
    ///     <em>its</em> return type, which is a different member's promise entirely.
    ///     <para>
    ///         <c>async</c> and iterator members need no case of their own and deliberately do not have
    ///         one: an <c>async</c> member's written return type is <c>Task&lt;int?&gt;</c>, never
    ///         <c>int?</c>, so <see cref="IsWritten" /> declines it, and an iterator cannot carry
    ///         <c>return expr;</c> at all (<c>CS1622</c>) so it is unreachable from here.
    ///     </para>
    /// </remarks>
    static TypeSyntax? WrittenReturnType(SyntaxNode position) {
        for (var node = position.Parent; node is not null; node = node.Parent) {
            switch (node) {
                case AnonymousFunctionExpressionSyntax:
                    return null;
                case LocalFunctionStatementSyntax local:
                    return local.ReturnType;
                case MethodDeclarationSyntax method:
                    return method.ReturnType;
                case AccessorDeclarationSyntax accessor:
                    return accessor.IsKind(SyntaxKind.GetAccessorDeclaration)
                        ? MemberType(accessor.Parent?.Parent)
                        : null;
                case BasePropertyDeclarationSyntax property:
                    return MemberType(property);

                // An operator, a conversion operator, a constructor and a finalizer all end the walk
                // without an answer: three of them have no return type to read and the operators are
                // left uncovered rather than guessed at.
                case MemberDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }

    static TypeSyntax? MemberType(SyntaxNode? member) =>
        member switch {
            PropertyDeclarationSyntax property => property.Type,
            IndexerDeclarationSyntax indexer => indexer.Type,
            _ => null
        };

    /// <summary>
    ///     Whether the argument's parameter writes the nullable type <em>and</em> the call without the
    ///     wrapper reaches the same symbol.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         The parameter's type is not enough on its own, and that is the point of the second
    ///         question.
    ///     </b> With <c>void M(int x)</c> and <c>void M(int? x)</c> both in scope,
    ///     <c>M(new int?(5))</c> calls the second and <c>M(5)</c> calls the first — a behaviour change
    ///     hiding inside a deletion. So the call is rebuilt without the wrapper and rebound, and only
    ///     an identical symbol counts.
    ///     <para>
    ///         ⚠ <see cref="SpeculativeBinding.CanBindDetached" /> is asked first because the rewrite is
    ///         bound away from its tree: a <c>MemberBindingExpressionSyntax</c> whose conditional access
    ///         does not travel with it makes the compiler throw inside the analyzer, which is
    ///         <c>AD0001</c> and therefore nothing at all in a report.
    ///     </para>
    /// </remarks>
    static bool ArgumentKeepsItsCall(
        SemanticModel model,
        ArgumentSyntax argument,
        ObjectCreationExpressionSyntax creation,
        ExpressionSyntax operand,
        INamedTypeSymbol nullable,
        CancellationToken cancellation
    ) {
        if (model.GetOperation(argument, cancellation) is not IArgumentOperation { Parameter: { } parameter }
            || !SymbolEqualityComparer.Default.Equals(parameter.Type, nullable)
            || argument.Parent?.Parent is not ExpressionSyntax call
            || model.GetSymbolInfo(call, cancellation).Symbol is not { } bound) {
            return false;
        }

        var rewritten = call.ReplaceNode(creation, operand);
        if (!SpeculativeBinding.CanBindDetached(rewritten)) {
            return false;
        }

        var speculated = model.GetSpeculativeSymbolInfo(
            call.SpanStart,
            rewritten,
            SpeculativeBindingOption.BindAsExpression
        );

        return SymbolEqualityComparer.IncludeNullability.Equals(speculated.Symbol, bound);
    }
}
