using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1132</c> — <c>t.Item1</c> where the element at that position has a name.
/// </summary>
/// <remarks>
///     <para>
///         The fix is one written token: <c>Item1</c> and the element's name are two
///         <see cref="IFieldSymbol" />s over one storage slot of one <c>ValueTuple</c>, and the rule
///         reports only when the rename is proved to bind back to that slot.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The name is read off the type of the receiver <em>at this site</em>, which is the
///             containing type of the field <c>Item1</c> binds to.
///         </b> One value can be seen through two tuple types with different names —
///         <c>(int X, int Y) b = a;</c> where <c>a</c> is <c>(int A, int B)</c> — and only the
///         receiver's own type says which names <c>b.</c> can be followed by.
///     </para>
///     <para>
///         ⚠ <b>An inferred name is a name, and could not be told apart if it were not.</b> Since C# 7.1
///         <c>var t = (a, b);</c> gives <c>t</c> the type <c>(int a, int b)</c>, and on that type
///         <see cref="IFieldSymbol.IsExplicitlyNamedTupleElement" /> answers <c>true</c> for <c>a</c> —
///         measured, not assumed — so the local's type carries no trace of where its names came from.
///         <c>t.a</c> binds, and it is the better spelling for the same reason any other name is.
///     </para>
///     <para>
///         ⚠
///         <b>
///             <c>Item8</c> and beyond are reached through <c>Rest</c> in metadata, and the semantic model
///             hides that completely.
///         </b> On a nine-element tuple <c>t.Item8</c> binds to a field of the
///         nine-element tuple type itself, and <see cref="INamedTypeSymbol.TupleElements" /> is flat:
///         the eighth element's <see cref="IFieldSymbol.CorrespondingTupleField" /> is that
///         <c>Item8</c>. <c>t.Rest.Item1</c> is a different expression whose receiver has type
///         <c>ValueTuple&lt;int, int&gt;</c>, which has no names, and is silent.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TupleElementByPositionAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.TupleElementByPosition);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.TupleElementByPosition);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    start.RegisterSyntaxNodeAction(
                        Analyze,
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxKind.MemberBindingExpression
                    );
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var node = (ExpressionSyntax)context.Node;
        var name = node switch {
            MemberAccessExpressionSyntax access => access.Name,
            MemberBindingExpressionSyntax binding => binding.Name,
            _ => null
        };

        // Cheapest test first: almost no member access is spelled `Item` and a number.
        if (name is not IdentifierNameSyntax identifier || !IsPositionalName(identifier.Identifier.ValueText)) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        // ⚠ A field of a tuple type. `System.Tuple<…>.Item1` is a *property* of a class and is the
        // only name it has; a `dynamic` receiver binds nothing. ⚠ Load-bearing beyond the concept:
        // `TupleElements` is a *default* array on a non-tuple type, so without this test a struct with
        // a field called `Item1` throws in `Element` — the sabotage reported AD0001, not a finding.
        //
        // ⚠ **A "the field is its own `CorrespondingTupleField`" check stood here and was removed as
        // dead.** It was meant to keep a named element out, and nothing can reach it: a field spelled
        // `ItemN` on a tuple type *is* the positional field — CS8125 forbids `ItemN` as a name at any
        // other position — and an element literally named `Item1` at position 1 is its own
        // corresponding field, measured. Deleting it turned no fixture red. `Element` comparing the
        // names is what declines that shape, and it has its own sabotage.
        if (model.GetSymbolInfo(node, cancellation).Symbol is not IFieldSymbol {
                ContainingType: { IsTupleType: true } tuple
            } field) {
            return;
        }

        if (Element(tuple, field) is not { } element) {
            return;
        }

        // ⚠ `nameof(t.Item1)` is the string "Item1". Renaming inside it changes a value, not a
        // spelling.
        if (InsideNameOf(node, model, cancellation)) {
            return;
        }

        // ⚠ SK1070 owns a complete run of `var x = t.ItemK;` reads: its fix replaces those lines with
        // a deconstruction, and this rule's rename would land inside them.
        if (node is MemberAccessExpressionSyntax read
            && TupleDeconstructionAnalyzer.IsReportedRead(read, model, cancellation)) {
            return;
        }

        var replacement = Spelling(element.Name);
        if (!BindsBack(node, replacement, tuple, element, model, cancellation)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                identifier.GetLocation(),
                FixEdits.Pack((identifier.Span, replacement)),
                "The tuple element `" + identifier.Identifier.ValueText + "` is named `" + element.Name + "`"
            )
        );
    }

    static bool IsPositionalName(string text) {
        if (text.Length < 5 || !text.StartsWith("Item", System.StringComparison.Ordinal) || text[4] == '0') {
            return false;
        }

        for (var i = 4; i < text.Length; i++) {
            if (text[i] is < '0' or > '9') {
                return false;
            }
        }

        return true;
    }

    /// <summary>The named element stored in <paramref name="field" />, or null when it has no other name.</summary>
    /// <remarks>
    ///     ⚠ <see cref="INamedTypeSymbol.TupleElements" /> lists the element whose name is <c>Item1</c> for
    ///     an unnamed position — <c>(int, string Name)</c> answers <c>Item1, Name</c> — so a position with
    ///     no name is declined by comparing the names, not by asking whether the element was named.
    /// </remarks>
    static IFieldSymbol? Element(INamedTypeSymbol tuple, IFieldSymbol field) {
        foreach (var element in tuple.TupleElements) {
            if (SymbolEqualityComparer.Default.Equals(element.CorrespondingTupleField, field)) {
                return element.Name != field.Name && element.Name.Length > 0 ? element : null;
            }
        }

        return null;
    }

    /// <summary>The element's name as it may be written after a <c>.</c>.</summary>
    /// <remarks>
    ///     ⚠ <c>(int @class, int b)</c> names its first element <c>class</c>. Only a reserved keyword needs
    ///     the <c>@</c>; a contextual one — <c>value</c>, <c>await</c>, <c>var</c> — is an ordinary
    ///     identifier after a dot.
    /// </remarks>
    static string Spelling(string name) =>
        SyntaxFacts.GetKeywordKind(name) is var kind && SyntaxFacts.IsReservedKeyword(kind) ? "@" + name : name;

    static bool InsideNameOf(SyntaxNode node, SemanticModel model, CancellationToken cancellation) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            if (current is InvocationExpressionSyntax {
                    Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" }
                } invocation
                && model.GetOperation(invocation, cancellation) is INameOfOperation) {
                return true;
            }

            if (current is StatementSyntax or MemberDeclarationSyntax) {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ Whether the renamed access is proved to reach the same storage slot.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Two proofs, because a member binding has no speculative spelling. First, member lookup:
    ///         the tuple type must hold exactly one member by the new name, and it must be the element.
    ///         The compiler already forbids the names that would collide with <c>ValueTuple</c>'s own
    ///         members (CS8126: <c>ToString</c>, <c>Equals</c>, <c>Rest</c>…), but <c>GetType</c> is not
    ///         on that list, so the lookup is asked rather than assumed.
    ///     </para>
    ///     <para>
    ///         Second, for <c>t.Item1</c>, the renamed expression is bound speculatively at the same
    ///         position and must land on a field whose corresponding positional field is the original.
    ///         That is the only check that would see an extension member or anything else the lookup on
    ///         the type itself does not.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>Both are stated gates, not load-bearing guards, and the sabotage says so.</b> Replacing
    ///         either with <c>true</c> turned no fixture red — including the <c>GetType</c> element,
    ///         because the element's field hides the inherited method and the rename does bind back. No
    ///         compiling program was found that reaches the decline. They are kept because
    ///         <c>fixIsSafe: true</c> is a promise that <c>--fix</c> may apply the edit unreviewed, and this is
    ///         the proof behind it rather than an argument from CS8125 and CS8126.
    ///     </para>
    /// </remarks>
    static bool BindsBack(
        ExpressionSyntax node,
        string replacement,
        INamedTypeSymbol tuple,
        IFieldSymbol element,
        SemanticModel model,
        CancellationToken cancellation
    ) {
        var members = tuple.GetMembers(element.Name);
        if (members.Length != 1 || !SymbolEqualityComparer.Default.Equals(members[0], element)) {
            return false;
        }

        if (node is not MemberAccessExpressionSyntax access) {
            return true;
        }

        var renamed = access.WithName(SyntaxFactory.IdentifierName(SyntaxFactory.ParseToken(replacement)));
        return model.GetSpeculativeSymbolInfo(access.SpanStart, renamed, SpeculativeBindingOption.BindAsExpression)
            .Symbol is IFieldSymbol bound
            && SymbolEqualityComparer.Default.Equals(bound.CorrespondingTupleField, element.CorrespondingTupleField);
    }
}
