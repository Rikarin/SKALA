using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2031</c> — a setter that does work and never reads <c>value</c>.
/// </summary>
/// <remarks>
///     The accessor's whole contract is to consume <c>value</c>. One that runs statements without ever
///     mentioning it stores something else, or stores nothing, and the assignment at the call site
///     succeeds silently either way.
///     <para>
///         ⚠ <b>The exemption is "announces the discard", not "discards".</b> A deliberate no-op setter
///         is real — interface satisfaction, a serializer that insists on a setter, a member kept for
///         compatibility — and it is written as <c>set { }</c>, as a <c>throw</c>, or on an
///         <c>[Obsolete]</c> member. Each of those is legible to the next reader as a decision. A setter
///         that assigns a field, calls a method or raises an event while ignoring <c>value</c> is not
///         announcing anything, and that is the whole line this rule draws.
///     </para>
///     <para>
///         ⚠ Purely syntactic, including the read. <c>value</c> cannot be shadowed inside a setter —
///         C# forbids a local of that name there — so "the identifier appears in the body" and "the
///         parameter is read" are the same question, and asking the semantic model would make the rule
///         need a project without making it more right.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnusedValueParameterAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.UnusedValueParameter);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.SetAccessorDeclaration,
            SyntaxKind.InitAccessorDeclaration
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var accessor = (AccessorDeclarationSyntax)context.Node;

        // No body at all: an auto-property, an abstract or extern declaration, an interface member,
        // a partial definition. There is nothing to have ignored `value`.
        SyntaxNode? body = (SyntaxNode?)accessor.Body ?? accessor.ExpressionBody?.Expression;
        if (body is null) {
            return;
        }

        // ⚠ The three shapes that announce the discard. An empty block says "writes go nowhere on
        // purpose"; so does a body that only throws. Statements a `#if` disabled are not in the tree,
        // so a setter emptied by the preprocessor reads as the first of these rather than as a bug.
        if (body is BlockSyntax { Statements.Count: 0 } || IsThrowOnly(body)) {
            return;
        }

        if (HasObsolete(accessor.AttributeLists) || HasObsolete(Member(accessor))) {
            return;
        }

        foreach (var node in body.DescendantNodesAndSelf()) {
            if (node is IdentifierNameSyntax { Identifier.ValueText: "value" }) {
                return;
            }
        }

        var kind = accessor.Keyword.ValueText;
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                accessor.Keyword.GetLocation(),
                "The `" + kind + "` accessor never reads `value`, so every assignment through it is discarded"
            )
        );
    }

    /// <summary>
    ///     Whether the body is nothing but a <c>throw</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Spelled with <c>Count</c> and an indexer rather than a list pattern. This assembly targets
    ///     <c>netstandard2.0</c> (ADR-006, so it loads into <c>csc</c> and into Rider), where
    ///     <c>System.Index</c> does not exist and a list pattern is a compile error rather than a style
    ///     choice.
    /// </remarks>
    static bool IsThrowOnly(SyntaxNode body) =>
        body is ThrowExpressionSyntax
        || body is BlockSyntax { Statements.Count: 1 } block
        && block.Statements[0] is ThrowStatementSyntax;

    /// <summary>The attribute lists of the property, indexer or event this accessor belongs to.</summary>
    static SyntaxList<AttributeListSyntax> Member(AccessorDeclarationSyntax accessor) =>
        accessor.Parent?.Parent is BasePropertyDeclarationSyntax property
            ? property.AttributeLists
            : default;

    /// <summary>
    ///     ⚠ By simple name, because the rule is syntactic. <c>[Obsolete]</c> written through an alias
    ///     is missed and that costs a finding; asking the semantic model would cost the rule its ability
    ///     to run without a project, which is worth more.
    /// </summary>
    static bool HasObsolete(SyntaxList<AttributeListSyntax> lists) {
        foreach (var list in lists) {
            foreach (var attribute in list.Attributes) {
                var name = attribute.Name switch {
                    QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
                    SimpleNameSyntax simple => simple.Identifier.ValueText,
                    _ => null
                };

                if (string.Equals(name, "Obsolete", StringComparison.Ordinal)
                    || string.Equals(name, "ObsoleteAttribute", StringComparison.Ordinal)) {
                    return true;
                }
            }
        }

        return false;
    }
}
