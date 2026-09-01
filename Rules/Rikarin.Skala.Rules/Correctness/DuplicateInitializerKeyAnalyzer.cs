using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
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
///         ⚠ <b>The comparer is not resolved. The rule declines whenever the constructor is given any
///         argument at all.</b> Key equality belongs to the collection's comparer and not to the key
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
                var lookups = new List<INamedTypeSymbol>();
                foreach (var name in Lookups) {
                    if (start.Compilation.GetTypeByMetadataName(name) is { } type) {
                        lookups.Add(type);
                    }
                }

                if (lookups.Count == 0) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, lookups),
                    SyntaxKind.ObjectCreationExpression,
                    SyntaxKind.ImplicitObjectCreationExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, List<INamedTypeSymbol> lookups) {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (creation.Initializer is not { } initializer || initializer.Expressions.Count < 2) {
            return;
        }

        // ⚠ The comparer question, answered by declining rather than by resolving. See the type
        // remarks: any argument at all withdraws the finding.
        if (creation.ArgumentList is { Arguments.Count: > 0 }) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (model.GetTypeInfo(creation, cancellation).Type is not INamedTypeSymbol collection
            || collection.TypeKind == TypeKind.Error
            || !Contains(lookups, collection)
            || collection.TypeArguments.Length is not (1 or 2)) {
            return;
        }

        var isSet = collection.TypeArguments.Length == 1;
        var keyType = collection.TypeArguments[0];
        if (!IsDecidable(keyType)) {
            return;
        }

        var seen = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);
        foreach (var element in initializer.Expressions) {
            var (key, form) = KeyOf(element, isSet);
            if (key is null || Normalize(model, key, cancellation) is not { } normalized) {
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
                RawKind: (int)SyntaxKind.ComplexElementInitializerExpression, Expressions.Count: 2
            } complex => (complex.Expressions[0], Form.Add),
            AssignmentExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                Left: ImplicitElementAccessSyntax { ArgumentList.Arguments.Count: 1 } access
            } => (access.ArgumentList.Arguments[0].Expression, Form.Indexer),
            InitializerExpressionSyntax or AssignmentExpressionSyntax => (null, Form.Add),
            _ => isSet ? (element, Form.Element) : (null, Form.Add)
        };

    /// <summary>
    ///     A key type whose default equality this analyzer can decide from constants alone.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>double</c>, <c>float</c> and <c>decimal</c> are excluded even though their boxed
    ///     <c>Equals</c> happens to agree with <c>EqualityComparer&lt;T&gt;.Default</c>: a rule that
    ///     reports a duplicate <c>NaN</c> or a duplicate <c>-0.0</c> is arguing about a key nobody
    ///     writes, and the exclusion costs nothing. Every reference type other than <c>string</c> is
    ///     excluded because a constant of one can only be <c>null</c>, and <c>Nullable&lt;T&gt;</c>
    ///     with it.
    /// </remarks>
    static bool IsDecidable(ITypeSymbol keyType) =>
        keyType.TypeKind == TypeKind.Enum
        || keyType.SpecialType is SpecialType.System_String
            or SpecialType.System_Boolean
            or SpecialType.System_Char
            or SpecialType.System_SByte
            or SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64;

    /// <summary>
    ///     A constant key as a comparable string, or null when the key is not a constant this rule
    ///     decides.
    /// </summary>
    /// <remarks>
    ///     ⚠ The numeric constants are normalised to one spelling on purpose. In a
    ///     <c>Dictionary&lt;long, V&gt;</c> the constants <c>1</c> and <c>1L</c> are boxed as
    ///     <c>int</c> and <c>long</c>, and comparing the boxes would say they differ where the
    ///     dictionary says they are one key. Every entry in one initializer converts to the same key
    ///     type, so collapsing them all through the invariant decimal spelling is exact.
    /// </remarks>
    static string? Normalize(SemanticModel model, ExpressionSyntax key, CancellationToken cancellation) {
        var constant = model.GetConstantValue(key, cancellation);
        if (!constant.HasValue) {
            return null;
        }

        return constant.Value switch {
            null => "null",
            string text => "s:" + text,
            bool flag => flag ? "b:1" : "b:0",
            char character => "n:" + ((long)character).ToString(CultureInfo.InvariantCulture),
            ulong unsigned => "n:" + unsigned.ToString(CultureInfo.InvariantCulture),
            sbyte or byte or short or ushort or int or uint or long =>
                "n:" + Convert.ToInt64(constant.Value, CultureInfo.InvariantCulture)
                    .ToString(CultureInfo.InvariantCulture),
            _ => null
        };
    }

    static bool Contains(List<INamedTypeSymbol> lookups, INamedTypeSymbol type) {
        foreach (var candidate in lookups) {
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, candidate)) {
                return true;
            }
        }

        return false;
    }
}
