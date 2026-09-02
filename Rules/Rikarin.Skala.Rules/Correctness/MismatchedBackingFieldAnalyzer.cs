using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2132</c> — an accessor that reads or writes a field belonging to a different property.
/// </summary>
/// <remarks>
///     The copy-paste defect in hand-written properties: the second property is written by duplicating
///     the first and one of the two field names is not changed. The type compiles, the property runs,
///     and it returns somebody else's value.
///     <para>
///         ⚠
///         <b>
///             The whole difficulty is that a name match between a property and a field is a
///             convention, not a fact, and plenty of correct code deliberately breaks it.
///         </b>
///         <c>Count</c> over <c>_items</c> and <c>Value</c> over <c>_inner</c> are ordinary
///         indirection, and a rule that treated "this accessor touches a field whose name is not mine"
///         as the finding would report both. Two conditions must therefore hold together before
///         anything is said, and they are conditions about <em>two</em> properties rather than one:
///         <list type="number">
///             <item>
///                 the property being examined <b>has</b> a conventionally named field of its own, of
///                 exactly its own type — so the author was following the convention here, and
///                 <c>Count</c> over <c>_items</c> is out because no <c>_count</c> exists; and
///             </item>
///             <item>
///                 the field the accessor actually touches is the conventionally named field of a
///                 <b>different property of the same type</b> — so <c>_inner</c> behind <c>Value</c> is
///                 out unless there is also an <c>Inner</c> property, at which point the two names have
///                 been crossed rather than chosen.
///             </item>
///         </list>
///     </para>
///     <para>
///         ⚠ <b>The accessor must be nothing but the field access.</b> <c>get =&gt; _items.Count;</c>
///         and a getter that logs before returning are declined by the shape test rather than by a
///         list of exceptions: the moment an accessor does something in addition to reaching for
///         storage, what it reaches for is a decision rather than a name that was mistyped.
///     </para>
///     <para>
///         The fix rewrites the field reference to the property's own field, and is <b>not</b> marked
///         safe — it is the one repair in this batch that changes what the program computes, which is
///         the entire point of the finding and exactly why a person must confirm the direction. The
///         author may equally have meant to rename the property.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MismatchedBackingFieldAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.MismatchedBackingField);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.PropertyDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var property = (PropertyDeclarationSyntax)context.Node;
        if (property.AccessorList is null) {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(property, context.CancellationToken) is not IPropertySymbol symbol
            || symbol.ContainingType is null) {
            return;
        }

        // ⚠ Not sabotage-testable, and that is a property of the rule rather than a gap in the
        // fixtures: `own` is the name the fix writes, so there is nothing to report without it. It
        // is also the first of the two conditions — the one that declines `Count` over `_items`.
        var own = Conventional(symbol);
        if (own is null) {
            return;
        }

        foreach (var accessor in property.AccessorList.Accessors) {
            var reference = accessor.IsKind(SyntaxKind.GetAccessorDeclaration)
                ? Returned(accessor)
                : accessor.IsKind(SyntaxKind.SetAccessorDeclaration)
                    || accessor.IsKind(SyntaxKind.InitAccessorDeclaration)
                    ? Written(accessor)
                    : null;

            if (reference is null) {
                continue;
            }

            // ⚠ `touched == own` survived a sabotage that turned nothing red, and it is kept anyway
            // rather than deleted. `OwnerOf` skips the property being examined, so in every ordinary
            // shape an accessor using its own field already finds no other owner — which is why the
            // sabotage was green. It is not green in one case: a single field can be conventional
            // for two properties when one of them is spelled with a leading underscore (`_Name`
            // backs both `Name` and `_Name`), and without this test that produces a finding whose
            // fix replaces a name with itself — a `skala fix` loop rather than a repair.
            if (context.SemanticModel.GetSymbolInfo(reference, context.CancellationToken).Symbol
                is not IFieldSymbol touched
                || SymbolEqualityComparer.Default.Equals(touched, own)
                || !SymbolEqualityComparer.Default.Equals(touched.ContainingType, symbol.ContainingType)) {
                continue;
            }

            var owner = OwnerOf(touched, symbol);
            if (owner is null) {
                continue;
            }

            var name = reference is MemberAccessExpressionSyntax member ? member.Name : reference;

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    name.GetLocation(),
                    FixEdits.Pack((name.Span, own.Name)),
                    "`"
                    + symbol.Name
                    + "`'s `"
                    + (accessor.IsKind(SyntaxKind.GetAccessorDeclaration) ? "get" : "set")
                    + "` uses `"
                    + touched.Name
                    + "`, which backs `"
                    + owner.Name
                    + "`; `"
                    + symbol.Name
                    + "` has its own `"
                    + own.Name
                    + "`"
                )
            );
        }
    }

    /// <summary>The whole of a getter, when the whole of it is one expression.</summary>
    static ExpressionSyntax? Returned(AccessorDeclarationSyntax accessor) {
        if (accessor.ExpressionBody is { } arrow) {
            return Storage(arrow.Expression);
        }

        // ⚠ Written out rather than as a list pattern: this project is netstandard2.0, where
        // `System.Index` does not exist and a list pattern is CS0518.
        return accessor.Body is { } body
            && body.Statements.Count == 1
            && body.Statements[0] is ReturnStatementSyntax { Expression: { } returned }
                ? Storage(returned)
                : null;
    }

    /// <summary>The assignment target of a setter, when the whole of it is <c>field = value;</c>.</summary>
    static ExpressionSyntax? Written(AccessorDeclarationSyntax accessor) {
        var expression = accessor.ExpressionBody?.Expression;
        if (expression is null
            && accessor.Body is { } body
            && body.Statements.Count == 1
            && body.Statements[0] is ExpressionStatementSyntax statement) {
            expression = statement.Expression;
        }

        return expression is AssignmentExpressionSyntax {
            RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
            Right: IdentifierNameSyntax { Identifier.ValueText: "value" }
        } assignment
                ? Storage(assignment.Left)
                : null;
    }

    /// <summary>
    ///     A bare field reference — <c>_name</c> or <c>this._name</c> — and nothing else.
    /// </summary>
    static ExpressionSyntax? Storage(ExpressionSyntax expression) =>
        expression switch {
            IdentifierNameSyntax => expression,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax } => expression,
            _ => null
        };

    /// <summary>
    ///     The property of this type that <paramref name="field" /> conventionally backs, when that is a
    ///     property other than <paramref name="examined" />.
    /// </summary>
    static IPropertySymbol? OwnerOf(IFieldSymbol field, IPropertySymbol examined) {
        foreach (var member in field.ContainingType.GetMembers().OfType<IPropertySymbol>()) {
            if (SymbolEqualityComparer.Default.Equals(member, examined)) {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(Conventional(member), field)) {
                return member;
            }
        }

        return null;
    }

    /// <summary>
    ///     The field of the declaring type that this property is conventionally backed by: the same
    ///     name under one of the four spellings people use, and <b>exactly</b> the property's type.
    /// </summary>
    /// <remarks>
    ///     ⚠ The type equality is not decoration. Without it, <c>_name</c> as a <c>List&lt;string&gt;</c>
    ///     behind a <c>string Name</c> would count as "the property's own field", and the rule would
    ///     start proposing a fix that does not compile — which the fix harness would catch, but only
    ///     after the finding had already been wrong.
    /// </remarks>
    static IFieldSymbol? Conventional(IPropertySymbol property) {
        if (property.Name.Length == 0) {
            return null;
        }

        var camel = char.ToLowerInvariant(property.Name[0]) + property.Name.Substring(1);
        var names = new[] { "_" + camel, camel, "m_" + camel, "_" + property.Name };

        foreach (var member in property.ContainingType.GetMembers().OfType<IFieldSymbol>()) {
            if (member.IsStatic != property.IsStatic || member.IsConst) {
                continue;
            }

            if (Array.Exists(names, name => string.Equals(name, member.Name, StringComparison.Ordinal))
                && SymbolEqualityComparer.Default.Equals(member.Type, property.Type)) {
                return member;
            }
        }

        return null;
    }
}
