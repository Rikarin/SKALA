using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1072</c> — <c>[.. new[] { a, b }, c]</c> is <c>[a, b, c]</c>.
/// </summary>
/// <remarks>
///     <para>
///         The array exists for the length of one expression: it is built so that the spread can take it
///         apart again. <c>SK1001</c> turns a <c>new</c> at a declaration into a collection expression;
///         this is the cleanup that follows it, where the <c>new</c> has ended up <em>inside</em> one.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The element types have to be identical, and that is not fussiness about a widening
///             nobody would notice.
///         </b> Before the rewrite an element is converted twice — to the array's
///         element type, then from there to the outer collection's. After it, once. Two conversions in
///         sequence and one in their place are not the same conversion: <c>new long[] { anInt }</c>
///         spread into a <c>double[]</c> goes <c>int → long → double</c> and would go
///         <c>
/// int →
///         double
///         </c>, and where a user-defined conversion is involved the single step does not exist at
///         all, because C# never chains two of them. Requiring the two element types to be the same
///         symbol makes both sides one conversion and removes the whole question.
///     </para>
///     <para>
///         ⚠ Only an array creation carrying an initializer is matched — <c>new T[] { … }</c> and
///         <c>new[] { … }</c>. A nested collection expression as the spread operand has no type of its
///         own to compare, and a spread of anything that was not created on the spot is a spread of
///         something the program can still see.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantSpreadElementAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.RedundantSpreadElement);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RedundantSpreadElement);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SpreadElement);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var spread = (SpreadElementSyntax)context.Node;
        if (spread.Parent is not CollectionExpressionSyntax outer) {
            return;
        }

        var initializer = spread.Expression switch {
            ArrayCreationExpressionSyntax { Initializer: { } written } => written,
            ImplicitArrayCreationExpressionSyntax implicitly => implicitly.Initializer,
            _ => null
        };

        // ⚠ An empty one is not this rule. `[.. new T[0], a]` is `[a]`, which means deleting a
        // separator as well as an element, and the empty-collection concept belongs to `SK1073`
        // and to `CA1825`.
        if (initializer is not { Expressions.Count: > 0 }) {
            return;
        }

        foreach (var element in initializer.Expressions) {
            if (element is InitializerExpressionSyntax) {
                return;
            }
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetTypeInfo(spread.Expression, cancellation).Type is not IArrayTypeSymbol {
                IsSZArray: true
            } created) {
            return;
        }

        var info = model.GetTypeInfo(outer, cancellation);
        if (ElementTypeOf(info.ConvertedType ?? info.Type) is not { } target
            || !SymbolEqualityComparer.Default.Equals(created.ElementType, target)) {
            return;
        }

        var first = initializer.Expressions[0];
        var last = initializer.Expressions[initializer.Expressions.Count - 1];
        var head = TextSpan.FromBounds(spread.SpanStart, first.SpanStart);
        var tail = TextSpan.FromBounds(last.Span.End, spread.Span.End);
        if (RewriteGuards.ContainsCommentOrDirective(spread.SyntaxTree, head)
            || RewriteGuards.ContainsCommentOrDirective(spread.SyntaxTree, tail)) {
            return;
        }

        // ⚠ The elements are carried across as *source text*, not rebuilt from the syntax nodes, so
        // whatever a person wrote between them — a comment, a line break they lined up — survives.
        var elements = spread.SyntaxTree.GetText(cancellation)
            .ToString(TextSpan.FromBounds(first.SpanStart, last.Span.End));

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                spread.GetLocation(),
                FixEdits.Pack((spread.Span, elements)),
                "The array is created so that the spread can take it apart again: `"
                + RewriteGuards.Trim(elements)
                + "`"
            )
        );
    }

    /// <summary>
    ///     What one element of this collection type is, or null when that is not decidable.
    /// </summary>
    /// <remarks>
    ///     ⚠ Ambiguity is answered with null rather than with a first match. A type implementing
    ///     <c>IEnumerable&lt;T&gt;</c> twice has two element types and no reason to prefer either, and
    ///     the whole point of this question is a comparison that has to be exact.
    /// </remarks>
    static ITypeSymbol? ElementTypeOf(ITypeSymbol? type) {
        switch (type) {
            case IArrayTypeSymbol { IsSZArray: true } array:
                return array.ElementType;

            case INamedTypeSymbol named:
                if (named.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T) {
                    return named.TypeArguments[0];
                }

                // `Span<T>` and `ReadOnlySpan<T>` are collection-expression targets and implement no
                // `IEnumerable<T>` to read the element type out of.
                if (named.TypeArguments.Length == 1
                    && named.OriginalDefinition.ToDisplayString() is "System.Span<T>" or "System.ReadOnlySpan<T>") {
                    return named.TypeArguments[0];
                }

                ITypeSymbol? found = null;
                foreach (var candidate in named.AllInterfaces) {
                    if (candidate.OriginalDefinition.SpecialType
                        != SpecialType.System_Collections_Generic_IEnumerable_T) {
                        continue;
                    }

                    if (found is not null) {
                        return null;
                    }

                    found = candidate.TypeArguments[0];
                }

                return found;

            default:
                return null;
        }
    }
}
