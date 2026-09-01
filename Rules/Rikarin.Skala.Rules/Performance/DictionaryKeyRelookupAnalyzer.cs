using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>
///     <c>SK4031</c> — a <c>foreach</c> over <c>dict.Keys</c> that indexes <c>dict</c> with the key it
///     was just handed.
/// </summary>
/// <remarks>
///     <para>
///         The loop is already standing on the entry. Reading <c>dict[key]</c> hashes the key again,
///         probes the bucket chain again and compares again, once per element — work the enumerator
///         did on the way past. Iterating the dictionary itself and taking the deconstructed value is
///         one pass and no hashing at all. <c>SK1033</c> reports the <c>ContainsKey</c>-then-index
///         double lookup; this is the same waste inside a loop, where it is multiplied by the count.
///     </para>
///     <para>
///         ⚠ The fix deconstructs — <c>foreach (var (key, value) in dict)</c> — rather than binding the
///         pair. That keeps <c>key</c> spelled the way the author spelled it, so every use of the key
///         that is <em>not</em> a lookup stays exactly as written and the edit touches only the header
///         and the indexer sites. Binding <c>entry</c> instead would mean rewriting every one of them
///         to <c>entry.Key</c>, which is more edits for no reader benefit.
///     </para>
///     <para>
///         ⚠ The receiver is matched against a closed list of framework dictionaries, because the
///         rewrite depends on <c>Keys</c> and the dictionary enumerating in the <em>same order</em>.
///         That holds for these types and is not a promise <c>IDictionary&lt;K, V&gt;</c> makes, so a
///         user implementation is left alone. <c>ConcurrentDictionary</c> is excluded for a second
///         reason as well: its <c>Keys</c> is a locked snapshot and iterating the table itself is not,
///         so the two loops genuinely see different things under concurrent writes. <c>SK4033</c> is
///         where that receiver's costs are reported.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DictionaryKeyRelookupAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.DictionaryKeyRelookupInLoop);

    /// <summary>
    ///     ⚠ Every one of these documents that its key collection and the dictionary enumerate the
    ///     same entries in the same order. That is the whole premise of the rewrite.
    /// </summary>
    static readonly string[] Dictionaries = [
        "System.Collections.Generic.Dictionary`2",
        "System.Collections.Generic.SortedDictionary`2",
        "System.Collections.Generic.SortedList`2",
        "System.Collections.ObjectModel.ReadOnlyDictionary`2",
        "System.Collections.Immutable.ImmutableDictionary`2",
        "System.Collections.Immutable.ImmutableSortedDictionary`2",
        "System.Collections.Frozen.FrozenDictionary`2"
    ];

    /// <summary>Names for the deconstructed value, in the order they are preferred.</summary>
    static readonly string[] ValueNames = ["value", "entryValue", "pairValue", "dictionaryValue"];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, "7.0")) {
                    return;
                }

                // ⚠ The fix writes a deconstruction, so `KeyValuePair<K, V>.Deconstruct` has to be
                // there. It arrived in .NET Core 2.0 and the analyzer targets netstandard2.0, so
                // "the framework this project builds against has it" is a question, not a given.
                var pair = start.Compilation.GetTypeByMetadataName("System.Collections.Generic.KeyValuePair`2");
                if (pair is null || !HasDeconstruct(pair)) {
                    return;
                }

                var dictionaries = new List<INamedTypeSymbol>();
                foreach (var name in Dictionaries) {
                    if (start.Compilation.GetTypeByMetadataName(name) is { } type) {
                        dictionaries.Add(type);
                    }
                }

                if (dictionaries.Count == 0) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, dictionaries),
                    SyntaxKind.ForEachStatement
                );
            }
        );
    }

    static bool HasDeconstruct(INamedTypeSymbol pair) {
        foreach (var member in pair.GetMembers("Deconstruct")) {
            if (member is IMethodSymbol { IsStatic: false, Parameters.Length: 2 } method
                && method.Parameters[0].RefKind == RefKind.Out
                && method.Parameters[1].RefKind == RefKind.Out) {
                return true;
            }
        }

        return false;
    }

    static void Analyze(SyntaxNodeAnalysisContext context, List<INamedTypeSymbol> dictionaries) {
        var loop = (ForEachStatementSyntax)context.Node;
        if (loop.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Name.Identifier.ValueText: "Keys"
            } keys
            || !CallShape.IsPlainNamePath(keys.Expression)) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        var dictionaryType = model.GetTypeInfo(keys.Expression, cancellation).Type;
        if (dictionaryType is not INamedTypeSymbol { TypeArguments.Length: 2 } dictionary
            || dictionary.TypeKind == TypeKind.Error
            || !Contains(dictionaries, dictionary)) {
            return;
        }

        // ⚠ The dictionary's text is left where it is and read once more by the indexer sites that
        // disappear, so what matters is that the *same storage* is meant both times. A property in
        // the path could hand back a different dictionary on the second read, and a local, a
        // parameter or a field cannot.
        var dictionarySymbol = model.GetSymbolInfo(keys.Expression, cancellation).Symbol;
        if (dictionarySymbol is not (ILocalSymbol or IParameterSymbol or IFieldSymbol)) {
            return;
        }

        if (model.GetDeclaredSymbol(loop, cancellation) is not { } key
            || !SymbolEqualityComparer.Default.Equals(key.Type, dictionary.TypeArguments[0])) {
            return;
        }

        var lookups = new List<ElementAccessExpressionSyntax>();
        foreach (var node in loop.Statement.DescendantNodes()) {
            switch (node) {
                case IdentifierNameSyntax name when Resolves(model, name, dictionarySymbol, cancellation):
                    // The loop rewrites nothing about the dictionary itself, so a body that
                    // reassigns it is a body where `dict` and `dict.Keys` are not the same object.
                    if (IsWritten(name)) {
                        return;
                    }

                    break;

                case ElementAccessExpressionSyntax access
                    when access.ArgumentList.Arguments.Count == 1
                    && access.ArgumentList.Arguments[0] is { RefKindKeyword.RawKind: (int)SyntaxKind.None } argument
                    && Resolves(model, argument.Expression, key, cancellation)
                    && Resolves(model, access.Expression, dictionarySymbol, cancellation):
                    // ⚠ `dict[key] = x` assigns through the indexer and `value` is a copy, so one
                    // write anywhere in the body withdraws the whole finding rather than the site.
                    if (IsWritten(access)) {
                        return;
                    }

                    lookups.Add(access);
                    break;
            }
        }

        if (lookups.Count == 0) {
            return;
        }

        var name1 = FreshValueName(loop);
        if (name1 is null) {
            return;
        }

        // ⚠ Three spans are replaced and each one could be holding a comment. The type, the
        // identifier and `.Keys` are one region in the header; every lookup is another.
        if (CallShape.ContainsComment(loop.Type)
            || CallShape.ContainsComment(keys)
            || !IsLayoutOnly(loop.Type.GetTrailingTrivia())) {
            return;
        }

        var edits = new List<(TextSpan Span, string Text)> {
            (
                TextSpan.FromBounds(loop.Type.SpanStart, loop.Identifier.Span.End),
                "var (" + loop.Identifier.ValueText + ", " + name1 + ")"
            ),
            (TextSpan.FromBounds(keys.OperatorToken.SpanStart, keys.Name.Span.End), string.Empty)
        };

        foreach (var lookup in lookups) {
            if (CallShape.ContainsComment(lookup)) {
                return;
            }

            edits.Add((lookup.Span, name1));
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(loop.SyntaxTree, TextSpan.FromBounds(loop.ForEachKeyword.SpanStart, keys.Span.End)),
                FixEdits.Pack([.. edits]),
                "The loop already holds this entry; `"
                + keys.Expression
                + "["
                + loop.Identifier.ValueText
                + "]` hashes and probes for it again, "
                + lookups.Count.ToString(CultureInfo.InvariantCulture)
                + " time(s) per iteration"
            )
        );
    }

    static bool Contains(List<INamedTypeSymbol> dictionaries, INamedTypeSymbol type) {
        foreach (var candidate in dictionaries) {
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, candidate)) {
                return true;
            }
        }

        return false;
    }

    static bool Resolves(
        SemanticModel model,
        ExpressionSyntax expression,
        ISymbol symbol,
        CancellationToken cancellation
    ) =>
        SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(expression, cancellation).Symbol, symbol);

    /// <summary>Whether the expression is assigned to, incremented, or passed by reference.</summary>
    static bool IsWritten(ExpressionSyntax expression) {
        SyntaxNode node = expression;
        while (node.Parent is ParenthesizedExpressionSyntax parentheses) {
            node = parentheses;
        }

        return node.Parent switch {
            AssignmentExpressionSyntax assignment => ReferenceEquals(assignment.Left, node),
            PrefixUnaryExpressionSyntax {
                RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression
            } => true,
            PostfixUnaryExpressionSyntax {
                RawKind: (int)SyntaxKind.PostIncrementExpression or (int)SyntaxKind.PostDecrementExpression
            } => true,
            ArgumentSyntax { RefKindKeyword.RawKind: not (int)SyntaxKind.None } => true,
            _ => false
        };
    }

    /// <summary>
    ///     A name for the deconstructed value that the enclosing body does not already use.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>value</c> reads best and is illegal inside a <c>set</c> or <c>init</c> accessor, where
    ///     it is already the implicit parameter and CS0136 follows whether or not the accessor
    ///     mentions it. The fallbacks exist so that the rule reports there rather than going quiet.
    /// </remarks>
    static string? FreshValueName(ForEachStatementSyntax loop) {
        var body = Enclosing(loop);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in body.DescendantTokens()) {
            if (token.IsKind(SyntaxKind.IdentifierToken)) {
                used.Add(token.ValueText);
            }
        }

        var inSetter = false;
        for (SyntaxNode? node = loop; node is not null; node = node.Parent) {
            if (node is AccessorDeclarationSyntax {
                    RawKind: (int)SyntaxKind.SetAccessorDeclaration or (int)SyntaxKind.InitAccessorDeclaration
                }) {
                inSetter = true;
                break;
            }
        }

        foreach (var candidate in ValueNames) {
            if (!used.Contains(candidate)
                && !(inSetter && string.Equals(candidate, "value", StringComparison.Ordinal))) {
                return candidate;
            }
        }

        return null;
    }

    static SyntaxNode Enclosing(SyntaxNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            if (current is BaseMethodDeclarationSyntax
                or AccessorDeclarationSyntax
                or LocalFunctionStatementSyntax
                or AnonymousFunctionExpressionSyntax
                or CompilationUnitSyntax) {
                return current;
            }
        }

        return node;
    }

    static bool IsLayoutOnly(SyntaxTriviaList trivia) {
        foreach (var item in trivia) {
            if (!item.IsKind(SyntaxKind.WhitespaceTrivia) && !item.IsKind(SyntaxKind.EndOfLineTrivia)) {
                return false;
            }
        }

        return true;
    }
}
