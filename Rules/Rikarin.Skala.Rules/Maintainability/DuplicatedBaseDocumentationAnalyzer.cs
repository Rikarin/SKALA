using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     <c>SK7100</c> — a documentation comment copied from the member it overrides or implements.
/// </summary>
/// <remarks>
///     ⚠ A copy is a copy until the next edit, and then it is a lie. The base member's prose is the one a
///     reader will find authoritative, and two paragraphs that were identical on the day they were written
///     have no mechanism keeping them so; <c>&lt;inheritdoc /&gt;</c> cannot drift because there is nothing
///     to drift from.
///     <para>
///         ⚠ <b>Identical, not similar.</b> The comparison is the two comments' text with whitespace
///         collapsed and the <c>///</c> markers removed, compared ordinally. Anything the author changed —
///         a word, an added <c>&lt;exception&gt;</c>, a different <c>&lt;param&gt;</c> — is prose that says
///         something the base does not, and replacing it with <c>&lt;inheritdoc /&gt;</c> would delete it.
///         There is no similarity threshold here, and there deliberately is not one.
///     </para>
///     <para>
///         ⚠ The comment is read from the trivia for a source base and from
///         <see cref="ISymbol.GetDocumentationCommentXml(System.Globalization.CultureInfo,bool,CancellationToken)" />
///         for one in metadata, and both are put through the same normaliser. A referenced assembly with no
///         XML documentation file beside it answers with nothing, which is silence rather than a match.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicatedBaseDocumentationAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor =
        SkalaRule.Descriptor(RuleIds.DocumentationDuplicatesBaseMember);

    static readonly ImmutableArray<SyntaxKind> Kinds = ImmutableArray.Create(
        SyntaxKind.MethodDeclaration,
        SyntaxKind.PropertyDeclaration,
        SyntaxKind.IndexerDeclaration,
        SyntaxKind.EventDeclaration
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, Kinds);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = context.Node;
        var cancellation = context.CancellationToken;

        // ⚠ Exactly one documentation trivia, and that is a guard rather than a simplification. The
        // fix replaces a span, and a member documented in two blocks with an ordinary comment between
        // them would have that comment deleted by the replacement.
        var documentation = declaration.GetLeadingTrivia().Where(IsDocumentation).ToList();
        if (documentation.Count != 1) {
            return;
        }

        var own = Normalize(documentation[0].ToFullString());
        if (own.Length == 0 || own.IndexOf("<inheritdoc", StringComparison.Ordinal) >= 0) {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, cancellation) is not { } symbol
            || Inherited(symbol) is not { } inherited
            || !string.Equals(own, Documentation(inherited, cancellation), StringComparison.Ordinal)) {
            return;
        }

        var text = declaration.SyntaxTree.GetText(cancellation);
        var span = documentation[0].FullSpan;

        // ⚠ The block's own line break is kept. Without it the replacement runs the member
        // declaration onto the end of the comment and the fix produces text that does not parse.
        var original = text.ToString(span);
        var ending = original.EndsWith("\r\n", StringComparison.Ordinal)
            ? "\r\n"
            : original.EndsWith("\n", StringComparison.Ordinal) ? "\n" : string.Empty;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(declaration.SyntaxTree, documentation[0].Span),
                FixEdits.Pack((span, "/// <inheritdoc />" + ending)),
                "The documentation repeats `"
                + inherited.ContainingType.Name
                + "."
                + inherited.Name
                + "`'s word for word; `<inheritdoc />` cannot drift from it"
            )
        );
    }

    static bool IsDocumentation(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);

    /// <summary>
    ///     The one member this declaration inherits its documentation from, or <c>null</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Three ways to have a base member and all three are covered: an <c>override</c>, an explicit
    ///     interface implementation, and an implicit one. The last is the one worth the search, because it
    ///     carries no syntax at all — a public method that happens to match an interface member — and it is
    ///     also where the answer must be unique. A method implementing <em>two</em> interface members
    ///     yields nothing here: <c>&lt;inheritdoc /&gt;</c> would have to pick one of them, and picking is
    ///     not a mechanical edit.
    /// </remarks>
    static ISymbol? Inherited(ISymbol symbol) {
        switch (symbol) {
            case IMethodSymbol { OverriddenMethod: { } method }:
                return method;

            case IPropertySymbol { OverriddenProperty: { } property }:
                return property;

            case IEventSymbol { OverriddenEvent: { } @event }:
                return @event;
        }

        var explicitly = symbol switch {
            IMethodSymbol method => method.ExplicitInterfaceImplementations.Cast<ISymbol>().ToList(),
            IPropertySymbol property => property.ExplicitInterfaceImplementations.Cast<ISymbol>().ToList(),
            IEventSymbol @event => @event.ExplicitInterfaceImplementations.Cast<ISymbol>().ToList(),
            _ => new List<ISymbol>()
        };
        if (explicitly.Count == 1) {
            return explicitly[0];
        }

        if (explicitly.Count > 0 || symbol.ContainingType is not { } container) {
            return null;
        }

        ISymbol? implicitly = null;
        foreach (var @interface in container.AllInterfaces) {
            foreach (var member in @interface.GetMembers()) {
                if (!SymbolEqualityComparer.Default.Equals(
                        container.FindImplementationForInterfaceMember(member),
                        symbol
                    )) {
                    continue;
                }

                if (implicitly is not null) {
                    return null;
                }

                implicitly = member;
            }
        }

        return implicitly;
    }

    /// <summary>The base member's documentation, from its source if it has one and from its XML if not.</summary>
    static string Documentation(ISymbol symbol, CancellationToken cancellation) {
        if (symbol.DeclaringSyntaxReferences.Length == 0) {
            return Normalize(symbol.GetDocumentationCommentXml(cancellationToken: cancellation) ?? string.Empty);
        }

        var node = symbol.DeclaringSyntaxReferences[0].GetSyntax(cancellation);
        var documentation = node.GetLeadingTrivia().Where(IsDocumentation).ToList();
        return documentation.Count == 1 ? Normalize(documentation[0].ToFullString()) : string.Empty;
    }

    /// <summary>
    ///     Both spellings of a documentation comment, reduced to the one string that can be compared.
    /// </summary>
    /// <remarks>
    ///     The source form carries <c>///</c> on every line and whatever indentation the file uses; the
    ///     metadata form carries a <c>&lt;member name="…"&gt;</c> wrapper whose attribute names the symbol
    ///     and therefore always differs. Stripping both and collapsing runs of whitespace leaves the
    ///     content, and the content is the whole of the question.
    /// </remarks>
    static string Normalize(string raw) {
        var builder = new StringBuilder();
        foreach (var line in raw.Split('\n')) {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("///", StringComparison.Ordinal)) {
                trimmed = trimmed.Substring(3).Trim();
            }

            if (trimmed.Length == 0) {
                continue;
            }

            if (builder.Length > 0) {
                builder.Append(' ');
            }

            builder.Append(trimmed);
        }

        var text = builder.ToString();
        if (!text.StartsWith("<member", StringComparison.Ordinal)) {
            return Collapse(text);
        }

        var open = text.IndexOf('>');
        var close = text.LastIndexOf("</member>", StringComparison.Ordinal);
        return open >= 0 && close > open ? Collapse(text.Substring(open + 1, close - open - 1).Trim()) : string.Empty;
    }

    static string Collapse(string text) {
        var builder = new StringBuilder(text.Length);
        var space = false;
        foreach (var character in text) {
            if (char.IsWhiteSpace(character)) {
                space = true;
                continue;
            }

            if (space && builder.Length > 0) {
                builder.Append(' ');
            }

            space = false;
            builder.Append(character);
        }

        return builder.ToString();
    }
}
