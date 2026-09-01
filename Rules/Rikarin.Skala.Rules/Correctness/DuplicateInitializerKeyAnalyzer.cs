using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Globalization;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2080</c> — a set or dictionary initializer that writes the same constant key twice.
/// </summary>
/// <remarks>
///     <para>
///         The three initializer forms fail three different ways. <c>{ { k, v }, { k, w } }</c> calls
///         <c>Add</c> twice and <c>Dictionary&lt;K, V&gt;.Add</c> throws <c>ArgumentException</c>;
///         <c>{ [k] = v, [k] = w }</c> assigns through the indexer and the second write silently wins;
///         <c>new HashSet&lt;T&gt; { a, a }</c> returns a set of one where two were written. What they
///         share is that the object at the end of the initializer is not the object on the page.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The comparer is not resolved. The rule declines whenever the constructor is given any
///             argument at all.
///         </b> Key equality belongs to the collection's comparer and not to the key
///         type — <c>new Dictionary&lt;string, int&gt;(StringComparer.OrdinalIgnoreCase)</c> throws on
///         <c>["a"]</c> and <c>["A"]</c>, which are distinct ordinally — and a comparer can equally
///         make two keys this rule believes equal into two entries. Declining on <em>any</em> argument
///         is broader than declining on a comparer argument on purpose: it costs a capacity-only
///         initializer a true finding, and in exchange the rule never has to decide which parameter a
///         comparer arrived through.
///     </para>
///     <para>
///         ⚠ The receiver is matched by symbol against a closed table, never by "it has an
///         <c>Add</c>". A <c>List&lt;T&gt;</c> may repeat an element deliberately and a user
///         collection's <c>Add</c> means whatever its author wrote.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicateInitializerKeyAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.DuplicateInitializerKey);

    /// <summary>
    ///     The collections whose keys are unique by construction, and which a collection initializer
    ///     can actually be written on.
    /// </summary>
    /// <remarks>
    ///     ⚠ The sorted types are here even though they compare rather than hash. The rule only ever
    ///     reports keys that are <em>equal</em> as constants, and two equal constants are equal under
    ///     the default <c>IComparer&lt;T&gt;</c> as well; the direction that would need care — two
    ///     distinct strings a culture comparer calls equal — is never reported.
    /// </remarks>
    static readonly string[] Lookups = [
        "System.Collections.Generic.Dictionary`2",
        "System.Collections.Generic.SortedDictionary`2",
        "System.Collections.Generic.SortedList`2",
        "System.Collections.Concurrent.ConcurrentDictionary`2",
        "System.Collections.Generic.HashSet`1",
        "System.Collections.Generic.SortedSet`1"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var lookups = CollectionShape.Resolve(start.Compilation, Lookups);
                if (lookups.Count == 0) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, lookups),
                    SyntaxKind.ObjectCreationExpression,
                    SyntaxKind.ImplicitObjectCreationExpression,
                    SyntaxKind.CollectionExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, List<INamedTypeSymbol> lookups) {
        // ⚠ Two spellings of one thing, and the second one is the idiom now. `new HashSet<T> { … }`
        // is a collection initializer whose entries are expressions; `HashSet<T> s = [ … ]` is a
        // collection expression, a different node kind entirely, and a rule registered only on the
        // first quietly says nothing about the second.
        // ⚠ The comparer question, answered by declining rather than by resolving. See the type
        // remarks: any constructor argument at all withdraws the finding. A collection expression
        // has none, so the question does not arise there.
        var entries = context.Node switch {
            CollectionExpressionSyntax expression => Elements(expression),
            BaseObjectCreationExpressionSyntax {
                ArgumentList: null or { Arguments.Count: 0 },
                Initializer: { } initializer
            } => Elements(initializer),
            _ => null
        };

        if (entries is null || entries.Count < 2) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        // ⚠ A collection expression has no natural type, so its only answer is the converted one; an
        // object creation's own type is the right answer, because a `new Dictionary<…>` assigned to
        // an `IDictionary<…>` converts to the interface and would fall out of the table.
        var typeInfo = model.GetTypeInfo(context.Node, cancellation);
        if ((typeInfo.Type ?? typeInfo.ConvertedType) is not INamedTypeSymbol collection
            || collection.TypeKind == TypeKind.Error
            || !CollectionShape.Contains(lookups, collection)
            || collection.TypeArguments.Length is not (1 or 2)) {
            return;
        }

        var isSet = collection.TypeArguments.Length == 1;
        var keyType = collection.TypeArguments[0];
        if (!CollectionShape.IsDecidableKeyType(keyType)) {
            return;
        }

        var seen = new Dictionary<string, ExpressionSyntax>(System.StringComparer.Ordinal);
        foreach (var element in entries) {
            var (key, form) = KeyOf(element, isSet);
            if (key is null || CollectionShape.ConstantKey(model, key, cancellation) is not { } normalized) {
                continue;
            }

            if (!seen.TryGetValue(normalized, out var first)) {
                seen[normalized] = key;
                continue;
            }

            var line = first.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    key.GetLocation(),
                    "`"
                    + key
                    + "` is already a key of this initializer, written on line "
                    + line.ToString(CultureInfo.InvariantCulture)
                    + "; "
                    + Consequence(form)
                )
            );
        }
    }

    /// <summary>
    ///     The entries of a collection expression, or null when it holds a spread.
    /// </summary>
    /// <remarks>
    ///     ⚠ A spread element contributes elements the analyzer cannot see, so one anywhere in the
    ///     expression withdraws the whole finding rather than being skipped over: a duplicate the
    ///     spread supplies is not this rule's to report, and one it hides is not this rule's to claim.
    ///     <para>
    ///         ⚠ The original nodes are handed back, never a rebuilt list. A
    ///         <c>SyntaxFactory.SeparatedList</c> over them produces a detached fragment, and the
    ///         semantic model answers nothing about a node that is not in its tree — a rule that did
    ///         that would go silent rather than wrong, which is the harder failure to notice.
    ///     </para>
    /// </remarks>
    static List<ExpressionSyntax>? Elements(CollectionExpressionSyntax expression) {
        var entries = new List<ExpressionSyntax>(expression.Elements.Count);
        foreach (var element in expression.Elements) {
            if (element is not ExpressionElementSyntax item) {
                return null;
            }

            entries.Add(item.Expression);
        }

        return entries;
    }

    static List<ExpressionSyntax> Elements(InitializerExpressionSyntax initializer) {
        var entries = new List<ExpressionSyntax>(initializer.Expressions.Count);
        foreach (var expression in initializer.Expressions) {
            entries.Add(expression);
        }

        return entries;
    }

    /// <summary>How the duplicate entry loses.</summary>
    enum Form {
        /// <summary><c>{ { k, v } }</c> — two <c>Add</c> calls.</summary>
        Add,

        /// <summary><c>{ [k] = v }</c> — two indexer assignments.</summary>
        Indexer,

        /// <summary><c>{ a }</c> on a set — two <c>Add</c> calls, the second returning false.</summary>
        Element
    }

    static string Consequence(Form form) =>
        form switch {
            Form.Add =>
                "the second `Add` of a key the collection already holds throws `ArgumentException` while the "
                + "object is being constructed",
            Form.Indexer => "the second assignment overwrites the first, whose value is evaluated and discarded",
            _ => "the second `Add` returns false and the set ends up one element shorter than it was written"
        };

    /// <summary>
    ///     The key expression of one initializer entry, or null when the entry is not keyed.
    /// </summary>
    /// <remarks>
    ///     ⚠ A plain expression is a key only for a set. A dictionary has no public
    ///     <c>Add(KeyValuePair&lt;K, V&gt;)</c>, so the shape does not arise there; and an
    ///     <c>ObjectInitializerExpression</c> entry whose left side is a plain name is a property
    ///     assignment — <c>Capacity</c> on a modern <c>Dictionary</c> — not an entry at all.
    /// </remarks>
    static (ExpressionSyntax? Key, Form Form) KeyOf(ExpressionSyntax element, bool isSet) =>
        element switch {
            InitializerExpressionSyntax {
                RawKind: (int)SyntaxKind.ComplexElementInitializerExpression,
                Expressions.Count: 2
            } complex => (complex.Expressions[0], Form.Add),
            AssignmentExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                Left: ImplicitElementAccessSyntax { ArgumentList.Arguments.Count: 1 } access
            } => (access.ArgumentList.Arguments[0].Expression, Form.Indexer),
            InitializerExpressionSyntax or AssignmentExpressionSyntax => (null, Form.Add),
            _ => isSet ? (element, Form.Element) : (null, Form.Add)
        };
}
