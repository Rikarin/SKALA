using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1060</c> — an element access that counts back from the collection's own size.
/// </summary>
/// <remarks>
///     ⚠ <b>The shape is trivial and the type test is the rule.</b> <c>xs[xs.Count - 1]</c> is
///     <c>xs[^1]</c> only where the language's implicit index support applies, and "has a
///     <c>Count</c>" is not that test: <c>IEnumerable&lt;T&gt;</c> has neither a countable member nor
///     an indexer, and <c>Dictionary&lt;int, V&gt;</c> has both and means something else entirely by
///     them. So the admitted set is read from the type — array, <c>string</c>, a real
///     <c>System.Index</c> indexer, or <c>IList&lt;T&gt;</c>/<c>IReadOnlyList&lt;T&gt;</c> — rather
///     than from the syntax.
///     <para>
///         ⚠ The subtrahend is admitted only as a positive integer literal or a plain <c>int</c> name
///         path. <c>^0</c> and a negative constant both reach <c>Index</c>'s constructor, which throws
///         a different exception than the subtraction it replaces.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IndexFromEndAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.IndexFromEnd);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.IndexFromEnd);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                // ⚠ Two gates, not one, and the second one is about *accessibility* rather than
                // existence. `System.Memory` ships an **internal** `System.Index` shim for
                // netstandard2.0, so `GetTypeByMetadataName` finds a symbol on a target framework
                // where `x[^1]` is `CS0518: predefined type 'System.Index' is not defined`. Checking
                // for null alone reported sixteen findings on Skala's own netstandard2.0 projects
                // whose fix did not compile; `IsSymbolAccessibleWithin` is the compiler's own test.
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)
                    || start.Compilation.GetTypeByMetadataName("System.Index") is not { } index
                    || !start.Compilation.IsSymbolAccessibleWithin(index, start.Compilation.Assembly)) {
                    return;
                }

                start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ElementAccessExpression);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var access = (ElementAccessExpressionSyntax)context.Node;
        if (access.ArgumentList.Arguments.Count != 1
            || access.ArgumentList.Arguments[0] is not { NameColon: null, RefKindKeyword.RawKind: 0 } argument) {
            return;
        }

        if (Unwrap(argument.Expression) is not BinaryExpressionSyntax {
                RawKind: (int)SyntaxKind.SubtractExpression
            } subtraction) {
            return;
        }

        if (Unwrap(subtraction.Left) is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } size) {
            return;
        }

        var member = size.Name.Identifier.ValueText;
        if (member is not ("Count" or "Length")) {
            return;
        }

        // ⚠ The two receivers must be the same text, and that text must be a chain of plain names.
        // `a[b.Count - 1]` is the defect this rule exists to make unwritable, and it must not be
        // "fixed" into `a[^1]`; an invoked receiver would also be evaluated once instead of twice.
        var receiver = access.Expression;
        if (!RewriteGuards.IsPlainNamePath(receiver)
            || !RewriteGuards.IsPlainNamePath(size.Expression)
            || !RewriteGuards.Same(receiver, size.Expression)) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetTypeInfo(receiver, cancellation).Type is not { } type
            || !SupportsIndexFromEnd(model.Compilation, type)) {
            return;
        }

        if (!IsAdmittedOffset(model, subtraction.Right, cancellation)
            || NullComparison.InsideExpressionTree(model, access, cancellation)
            || RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(access.SyntaxTree, argument.Expression.Span)) {
            return;
        }

        var replacement = "^" + Unwrap(subtraction.Right);
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(access.SyntaxTree, argument.Expression.Span),
                FixEdits.Pack((argument.Expression.Span, replacement)),
                "The index counts back from the collection's own size: `" + RewriteGuards.Trim(replacement) + "`"
            )
        );
    }

    /// <summary>
    ///     ⚠ Whether <c>x[^n]</c> both compiles for this type and means the last-<c>n</c>th element.
    /// </summary>
    /// <remarks>
    ///     The language's own test — countable, plus an <c>int</c> indexer — is wider than this one on
    ///     purpose. It admits <c>Dictionary&lt;int, V&gt;</c>, where <c>d[^1]</c> compiles, lowers to
    ///     the identical <c>d[d.Count - 1]</c>, and reads as an ordinal position the type does not
    ///     have. A rule that suggested it would be right about the program and wrong about the code.
    /// </remarks>
    static bool SupportsIndexFromEnd(Compilation compilation, ITypeSymbol type) {
        if (type.TypeKind == TypeKind.Error) {
            return false;
        }

        if (type is IArrayTypeSymbol { Rank: 1 } || type.SpecialType == SpecialType.System_String) {
            return true;
        }

        var systemIndex = compilation.GetTypeByMetadataName("System.Index");
        foreach (var candidate in type.GetMembers("this[]")) {
            if (candidate is IPropertySymbol { Parameters.Length: 1 } indexer
                && SymbolEqualityComparer.Default.Equals(indexer.Parameters[0].Type, systemIndex)) {
                return true;
            }
        }

        // ⚠ `Span<T>` is the counter-example to "a real `Index` indexer or a list interface". It has
        // neither — it is a `ref struct`, so it implements nothing, and its only indexer takes an
        // `int` — and `span[^1]` compiles anyway, through the same implicit index support the rule
        // otherwise declines to trust. It is named because the type is known, not inferred.
        var self = (type as INamedTypeSymbol)?.OriginalDefinition;
        if (self is not null
            && (SymbolEqualityComparer.Default.Equals(self, compilation.GetTypeByMetadataName("System.Span`1"))
                || SymbolEqualityComparer.Default.Equals(
                    self,
                    compilation.GetTypeByMetadataName("System.ReadOnlySpan`1")
                ))) {
            return true;
        }

        var list = compilation.GetTypeByMetadataName("System.Collections.Generic.IList`1");
        var readOnlyList = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyList`1");
        foreach (var implemented in type.AllInterfaces) {
            var definition = implemented.OriginalDefinition;
            if (SymbolEqualityComparer.Default.Equals(definition, list)
                || SymbolEqualityComparer.Default.Equals(definition, readOnlyList)) {
                return true;
            }
        }

        return type is INamedTypeSymbol named
            && (SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, list)
                || SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, readOnlyList));
    }

    /// <summary>
    ///     ⚠ Whether the amount subtracted may become the operand of <c>^</c>.
    /// </summary>
    /// <remarks>
    ///     <c>^n</c> is <c>new Index(n, fromEnd: true)</c>, whose constructor rejects a negative
    ///     <c>n</c> with <c>ArgumentOutOfRangeException</c>; the subtraction it replaces would have
    ///     reached the indexer and thrown <c>IndexOutOfRangeException</c>. And <c>^0</c> is
    ///     <c>Count</c>, which throws either way but says the opposite of what it looks like — the trap
    ///     ReSharper's own <c>ZeroIndexFromEnd</c> exists to report. Both are refused rather than
    ///     rewritten, so a positive literal or a plain <c>int</c> name is all that is admitted.
    /// </remarks>
    static bool IsAdmittedOffset(SemanticModel model, ExpressionSyntax offset, CancellationToken cancellation) {
        var expression = Unwrap(offset);
        var constant = model.GetConstantValue(expression, cancellation);
        if (constant is { HasValue: true, Value: int value }) {
            return value > 0;
        }

        if (constant.HasValue) {
            return false;
        }

        return RewriteGuards.IsPlainNamePath(expression)
            && model.GetTypeInfo(expression, cancellation).Type is { SpecialType: SpecialType.System_Int32 };
    }

    static ExpressionSyntax Unwrap(ExpressionSyntax expression) {
        while (expression is ParenthesizedExpressionSyntax parenthesized) {
            expression = parenthesized.Expression;
        }

        return expression;
    }
}
