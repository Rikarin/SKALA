using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>
///     <c>SK4030</c> — <c>list.FirstOrDefault(p)</c> is <c>list.Find(p)</c>.
/// </summary>
/// <remarks>
///     <para>
///         <c>List&lt;T&gt;</c> and <c>ImmutableList&lt;T&gt;</c> declare <c>Find</c>, <c>Exists</c> and
///         <c>TrueForAll</c>, which walk the backing array with an index. The <c>Enumerable</c>
///         extensions that answer the same question allocate an enumerator, drive it through
///         <c>MoveNext</c>, and call the predicate through a <c>Func&lt;T, bool&gt;</c> the collection
///         method takes as a <c>Predicate&lt;T&gt;</c> — the same delegate under another name.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Each pair was checked on the empty sequence, which is where these substitutions go
///             wrong.
///         </b> <c>All</c> returns <c>true</c> vacuously and <c>TrueForAll</c> returns
///         <c>true</c> over zero elements; <c>Any</c> and <c>Exists</c> both return <c>false</c>;
///         <c>FirstOrDefault</c> and <c>Find</c> both return <c>default(T)</c> — including for a value
///         type, where <c>First</c>/<c>Single</c> would have thrown and neither of these does. Nothing
///         in the table changes a return, which is why the fix is safe.
///     </para>
///     <para>
///         ⚠ The receiver's type is matched against <c>List&lt;T&gt;</c> and
///         <c>ImmutableList&lt;T&gt;</c> by symbol, never a member named <c>Exists</c> found by
///         lookup. A set carries a comparer and a user type carries whatever its author wrote, and
///         "this name exists" is not a proof that the two mean the same thing.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CollectionOwnMethodAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.CollectionOwnMethod);

    /// <summary>
    ///     The LINQ operator, and the member of the collection itself that answers it.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>First</c>, <c>Single</c> and <c>SingleOrDefault</c> are deliberately absent.
    ///     <c>Find</c> returns <c>default(T)</c> where <c>First</c> throws and where
    ///     <c>SingleOrDefault</c> throws on a second match, so those are three different programs
    ///     rather than three spellings of one.
    /// </remarks>
    static readonly (string Linq, string Own)[] Table = [
        ("FirstOrDefault", "Find"), ("Any", "Exists"), ("All", "TrueForAll")
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var enumerable = start.Compilation.GetTypeByMetadataName("System.Linq.Enumerable");
                if (enumerable is null) {
                    return;
                }

                var receivers = new List<INamedTypeSymbol>();
                foreach (var name in new[] {
                             "System.Collections.Generic.List`1", "System.Collections.Immutable.ImmutableList`1"
                         }) {
                    if (start.Compilation.GetTypeByMetadataName(name) is { } type) {
                        receivers.Add(type);
                    }
                }

                if (receivers.Count == 0) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, enumerable, receivers),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol enumerable,
        List<INamedTypeSymbol> receivers
    ) {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // ⚠ Plain member access only. `list?.Any(p)` binds through a MemberBindingExpression, and
        // `Enumerable.Any(list, p)` puts the collection in the argument list — the first is a
        // different node to edit and the second is a different edit entirely.
        if (invocation.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } access
            || invocation.ArgumentList.Arguments.Count != 1) {
            return;
        }

        var argument = invocation.ArgumentList.Arguments[0];

        // ⚠ A lambda or an anonymous method, never a delegate-typed expression. `Find` takes a
        // `Predicate<T>` and `FirstOrDefault` takes a `Func<T, bool>`; the two are unrelated
        // delegate types, so a `Func<T, bool>` *variable* handed to `Find` is CS1503. A lambda
        // converts to either, which is the whole reason the substitution is writable at all.
        if (argument.Expression is not (
                SimpleLambdaExpressionSyntax or ParenthesizedLambdaExpressionSyntax or AnonymousMethodExpressionSyntax
            )
            || argument.NameColon is not null) {
            return;
        }

        var name = access.Name.Identifier.ValueText;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol method) {
            return;
        }

        var definition = (method.ReducedFrom ?? method).OriginalDefinition;
        if (!SymbolEqualityComparer.Default.Equals(definition.ContainingType, enumerable)
            || definition.Parameters.Length != 2) {
            return;
        }

        var receiverType = model.GetTypeInfo(access.Expression, cancellation).Type;
        if (receiverType is not INamedTypeSymbol { TypeArguments.Length: 1 } collection
            || collection.TypeKind == TypeKind.Error
            || !Matches(collection, receivers)) {
            return;
        }

        var element = collection.TypeArguments[0];

        // `list.Any(x => x == v)` is `list.Contains(v)`: no delegate at all, and one comparison
        // written where the reader can see it. Tried before the predicate table, because the shape
        // is also an `Any` and `Contains` is the better of the two answers.
        //
        // ⚠ Falling *through* rather than returning is the point. This edit deletes the lambda, so
        // a comment inside it is content the fix would lose — and the answer there is not silence,
        // it is `Exists`, whose edit is one token and cannot lose anything.
        if (string.Equals(name, "Any", StringComparison.Ordinal)
            && !CallShape.ContainsComment(invocation.ArgumentList)
            && ContainsCandidate(model, argument.Expression, element, cancellation) is { } value) {
            ReportContains(context, invocation, access, value);
            return;
        }

        foreach (var (linq, own) in Table) {
            if (!string.Equals(name, linq, StringComparison.Ordinal)) {
                continue;
            }

            // ⚠ Looked up rather than assumed. The analyzer is netstandard2.0 and runs against
            // whatever the project targets; `ImmutableList<T>` gaining or losing a member is not
            // something a hard-coded table would notice, and the fix has to bind.
            if (!HasPredicateMethod(collection, own, element)) {
                return;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    access.Name.GetLocation(),
                    FixEdits.Pack((access.Name.Span, own)),
                    "`"
                    + collection.Name
                    + "."
                    + own
                    + "` walks the list directly; `"
                    + linq
                    + "` allocates an enumerator to do the same"
                )
            );
            return;
        }
    }

    static bool Matches(INamedTypeSymbol type, List<INamedTypeSymbol> receivers) {
        foreach (var receiver in receivers) {
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, receiver)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>A public instance <c>name(Predicate&lt;element&gt;)</c> on the collection.</summary>
    static bool HasPredicateMethod(INamedTypeSymbol collection, string name, ITypeSymbol element) {
        foreach (var member in collection.GetMembers(name)) {
            if (member is IMethodSymbol {
                    IsStatic: false,
                    DeclaredAccessibility: Accessibility.Public,
                    Parameters.Length: 1
                } method
                && method.Parameters[0].Type is INamedTypeSymbol {
                    Name: "Predicate",
                    TypeArguments.Length: 1
                } predicate
                && SymbolEqualityComparer.Default.Equals(predicate.TypeArguments[0], element)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     The compared-against expression of <c>x =&gt; x == v</c>, or null when the lambda is
    ///     anything else.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>Contains</c> compares with <c>EqualityComparer&lt;T&gt;.Default</c> and <c>==</c> is an
    ///     operator, so the two agree only for types where the default comparer <em>is</em> the
    ///     operator. <c>string</c>, the integral types, <c>char</c>, <c>bool</c>, <c>decimal</c> and
    ///     any <c>enum</c> qualify. <c>double</c> and <c>float</c> emphatically do not:
    ///     <c>NaN == NaN</c> is false and <c>EqualityComparer&lt;double&gt;.Default.Equals(NaN, NaN)</c>
    ///     is true, and <c>-0.0 == 0.0</c> is true where <c>Equals</c> says false. A reference type is
    ///     out for the mirror-image reason — <c>==</c> may be reference identity where the comparer
    ///     calls <c>Equals</c>, or may be an operator somebody wrote.
    /// </remarks>
    static ExpressionSyntax? ContainsCandidate(
        SemanticModel model,
        ExpressionSyntax lambda,
        ITypeSymbol element,
        CancellationToken cancellation
    ) {
        if (!IsComparableByOperator(element)
            || lambda is not SimpleLambdaExpressionSyntax {
                Body: BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression } equality
            } simple) {
            return null;
        }

        // ⚠ A user-defined `==` is a method call, and the default comparer would not call it.
        // `IsComparableByOperator` already excludes every type that can have one; this is the
        // second lock on the same door, because the first depends on the element type resolving.
        if (model.GetSymbolInfo(equality, cancellation).Symbol is IMethodSymbol {
                MethodKind: MethodKind.UserDefinedOperator
            }) {
            return null;
        }

        var parameter = model.GetDeclaredSymbol(simple.Parameter, cancellation);
        if (parameter is null) {
            return null;
        }

        var (self, other) = IsParameter(model, equality.Left, parameter, cancellation)
            ? (equality.Left, equality.Right)
            : IsParameter(model, equality.Right, parameter, cancellation)
                ? (equality.Right, equality.Left)
                : (null, null);

        if (self is null || other is null) {
            return null;
        }

        // `x => x == x` compares the parameter with itself, and `Contains` has nothing to be given.
        if (IsParameter(model, other, parameter, cancellation) || References(model, other, parameter, cancellation)) {
            return null;
        }

        // ⚠ The value moves out of the lambda and is evaluated once per call rather than once per
        // element — for a name path that is the same thing, and for anything else it is not.
        return CallShape.IsPlainNamePath(other) || other is LiteralExpressionSyntax ? other : null;
    }

    static bool IsParameter(
        SemanticModel model,
        ExpressionSyntax expression,
        ISymbol parameter,
        CancellationToken cancellation
    ) =>
        expression is IdentifierNameSyntax
        && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(expression, cancellation).Symbol, parameter);

    static bool References(
        SemanticModel model,
        ExpressionSyntax expression,
        ISymbol parameter,
        CancellationToken cancellation
    ) {
        foreach (var identifier in expression.DescendantNodesAndSelf()) {
            if (identifier is IdentifierNameSyntax name
                && SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(name, cancellation).Symbol,
                    parameter
                )) {
                return true;
            }
        }

        return false;
    }

    static bool IsComparableByOperator(ITypeSymbol element) =>
        element.TypeKind == TypeKind.Enum
        || element.SpecialType is SpecialType.System_String
            or SpecialType.System_Boolean
            or SpecialType.System_Char
            or SpecialType.System_SByte
            or SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Decimal;

    static void ReportContains(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax access,
        ExpressionSyntax value
    ) {
        var span = TextSpan.FromBounds(access.Name.SpanStart, invocation.Span.End);
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(invocation.SyntaxTree, span),
                FixEdits.Pack((span, "Contains(" + value + ")")),
                "`Contains(" + value + ")` is the same test with no delegate and no enumerator"
            )
        );
    }
}
