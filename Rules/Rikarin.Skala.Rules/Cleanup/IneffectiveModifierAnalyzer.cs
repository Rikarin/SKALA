using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Cleanup;

/// <summary><c>SK0241</c> — a modifier the language already applies, written out anyway.</summary>
/// <remarks>
///     <para>
///         Six shapes, each of which is a keyword restating what the enclosing declaration has already
///         said: <c>abstract</c> on an interface member, <c>sealed</c> on a member of a <c>sealed</c>
///         type, <c>class</c> after <c>record</c>, <c>: int</c> on an enum, <c>readonly</c> on a
///         member of a <c>readonly struct</c>, and <c>scoped</c> on an <c>out</c> parameter.
///     </para>
///     <para>
///         ⚠ <b>Every one of them is answered by a keyword that is written in the same file</b>, which
///         is why this rule is syntactic and runs on a loose file. Nothing here asks what a name binds
///         to: <c>sealed</c>, <c>readonly</c>, <c>interface</c> and <c>record</c> are keywords, and the
///         enum's <c>int</c> is matched only in its predefined-keyword spelling — <c>enum E : Int32</c>
///         is left alone, because <c>Int32</c> is an ordinary identifier that a using directive or a
///         user type can point somewhere else.
///     </para>
///     <para>
///         ⚠ <b>A nested type is never the subject.</b> A type declared inside an interface is not
///         implicitly abstract and a type nested in a <c>sealed</c> class is not implicitly sealed, so
///         the modifier there is the author's and deleting it would change what the program means.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IneffectiveModifierAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.IneffectiveModifier);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeMember,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.IndexerDeclaration,
            SyntaxKind.EventDeclaration,
            SyntaxKind.EventFieldDeclaration,
            SyntaxKind.OperatorDeclaration
        );

        context.RegisterSyntaxNodeAction(AnalyzeAccessor, SyntaxKind.GetAccessorDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeRecord, SyntaxKind.RecordDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeEnum, SyntaxKind.EnumDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeParameter, SyntaxKind.Parameter);
    }

    /// <summary>
    ///     <c>scoped</c> on an <c>out</c> parameter, which the language already applies.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b><c>out</c> is the only implicitly-scoped parameter form, and that was measured rather
    ///     than reasoned from the name.</b> Six spellings were put through
    ///     <c>jb inspectcode</c> 2025.2.6 — <c>scoped out int</c>, <c>scoped out</c> a <c>ref struct</c>,
    ///     <c>scoped ref</c> a <c>ref struct</c>, <c>scoped in</c> a <c>ref struct</c>, <c>scoped</c> on
    ///     a by-value <c>ref struct</c>, and <c>scoped ref int</c> — and ReSharper reported the two
    ///     <c>out</c> forms and nothing else. The others are not redundant: <c>scoped</c> on a
    ///     <c>ref</c> or <c>in</c> parameter narrows the <em>ref-safe-to-escape</em> of the reference
    ///     itself, and deleting it lets the reference escape where it previously could not.
    ///     <para>
    ///         ⚠ Purely syntactic, like the rest of this rule: <c>scoped</c> and <c>out</c> are both
    ///         tokens in the parameter's own modifier list, so nothing here has to bind a type — which
    ///         is what keeps <c>SK0241</c> running on a loose file.
    ///     </para>
    /// </remarks>
    static void AnalyzeParameter(SyntaxNodeAnalysisContext context) {
        var parameter = (ParameterSyntax)context.Node;
        if (Has(parameter.Modifiers, SyntaxKind.OutKeyword)
            && Find(parameter.Modifiers, SyntaxKind.ScopedKeyword) is { } scoped) {
            Report(
                context,
                scoped,
                "an `out` parameter is implicitly `scoped`, so writing the modifier narrows nothing"
            );
        }
    }

    /// <summary>
    ///     ⚠ The order matters only in that one member cannot carry two of these at once: a
    ///     <c>readonly struct</c> has no base to seal against and an interface member is not
    ///     <c>readonly</c>, so the first match is the only match.
    /// </summary>
    static void AnalyzeMember(SyntaxNodeAnalysisContext context) {
        var member = (MemberDeclarationSyntax)context.Node;
        if (member.Parent is not TypeDeclarationSyntax containing) {
            return;
        }

        // ⚠ `static` withdraws the interface half entirely, and getting this wrong would have been the
        // rule's worst false positive. A static interface member is *not* implicitly abstract — C# 11's
        // `static abstract T operator +(T, T)` is an abstract static member, and without the keyword the
        // same declaration is a static member that must have a body. Deleting `abstract` there does not
        // tidy anything; it changes the declaration into one that no longer compiles.
        if (containing.IsKind(SyntaxKind.InterfaceDeclaration)
            && !Has(member.Modifiers, SyntaxKind.StaticKeyword)
            && Find(member.Modifiers, SyntaxKind.AbstractKeyword) is { } abstractKeyword) {
            Report(
                context,
                abstractKeyword,
                "an interface member is abstract whether or not `abstract` is written, so the modifier says nothing"
            );

            return;
        }

        if (Has(containing.Modifiers, SyntaxKind.SealedKeyword)
            && Find(member.Modifiers, SyntaxKind.SealedKeyword) is { } sealedKeyword) {
            Report(
                context,
                sealedKeyword,
                "the containing type is `sealed`, so nothing can derive from it and nothing could have "
                + "overridden this member"
            );

            return;
        }

        // ⚠ Fields are excluded and that is not an oversight: CS8340 *requires* every instance field of
        // a readonly struct to be readonly, so the keyword there is the language's and not the
        // author's, and deleting it is an error rather than a cleanup.
        if (containing.IsKind(SyntaxKind.StructDeclaration)
            && Has(containing.Modifiers, SyntaxKind.ReadOnlyKeyword)
            && Find(member.Modifiers, SyntaxKind.ReadOnlyKeyword) is { } readOnlyKeyword) {
            Report(
                context,
                readOnlyKeyword,
                "every instance member of a `readonly struct` is already `readonly`"
            );
        }
    }

    /// <summary>
    ///     ⚠ Only the <c>get</c> accessor, and the shape is narrower than it looks.
    /// </summary>
    /// <remarks>
    ///     CS8664 rejects <c>readonly</c> on an accessor unless the property has <em>both</em> a get and
    ///     a set, and a set inside a <c>readonly struct</c> may not assign a field — so the only
    ///     declaration this branch can ever see is a get/set property in a readonly struct whose setter
    ///     does nothing to the instance. That is legal, it was measured rather than assumed, and it is
    ///     reported; a first attempt at the fixture used a get-only property and did not compile.
    /// </remarks>
    static void AnalyzeAccessor(SyntaxNodeAnalysisContext context) {
        var accessor = (AccessorDeclarationSyntax)context.Node;
        if (accessor.Parent?.Parent?.Parent is TypeDeclarationSyntax containing
            && containing.IsKind(SyntaxKind.StructDeclaration)
            && Has(containing.Modifiers, SyntaxKind.ReadOnlyKeyword)
            && Find(accessor.Modifiers, SyntaxKind.ReadOnlyKeyword) is { } keyword) {
            Report(context, keyword, "every instance member of a `readonly struct` is already `readonly`");
        }
    }

    static void AnalyzeRecord(SyntaxNodeAnalysisContext context) {
        var declaration = (RecordDeclarationSyntax)context.Node;
        if (declaration.ClassOrStructKeyword.IsKind(SyntaxKind.ClassKeyword)) {
            Report(context, declaration.ClassOrStructKeyword, "`record` already means `record class`");
        }
    }

    /// <summary>
    ///     ⚠ The <c>int</c> keyword only, never <c>System.Int32</c> or an alias for it.
    /// </summary>
    /// <remarks>
    ///     The written form is what decides here, because this rule has no semantic model to ask what
    ///     an identifier resolves to — and <c>Int32</c> is an identifier. The keyword cannot be aliased
    ///     or shadowed, so matching it is exact.
    /// </remarks>
    static void AnalyzeEnum(SyntaxNodeAnalysisContext context) {
        var declaration = (EnumDeclarationSyntax)context.Node;
        if (declaration.BaseList is not { Types.Count: 1 } baseList
            || baseList.Types[0].Type is not PredefinedTypeSyntax predefined
            || !predefined.Keyword.IsKind(SyntaxKind.IntKeyword)
            || !IsWhitespaceOnly(declaration.Identifier.TrailingTrivia)
            || !IsWhitespaceOnly(baseList)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                baseList.GetLocation(),
                FixEdits.Pack((TextSpan.FromBounds(declaration.Identifier.Span.End, baseList.Span.End), string.Empty)),
                "`int` is the underlying type an enum has when none is written"
            )
        );
    }

    static SyntaxToken? Find(SyntaxTokenList modifiers, SyntaxKind kind) {
        foreach (var modifier in modifiers) {
            if (modifier.IsKind(kind)) {
                return modifier;
            }
        }

        return null;
    }

    static bool Has(SyntaxTokenList modifiers, SyntaxKind kind) => Find(modifiers, kind) is not null;

    /// <summary>
    ///     Deletes the keyword and the space after it, never the trivia in front of it.
    /// </summary>
    /// <remarks>
    ///     ⚠ The span runs from the keyword to the <em>next token</em>, so the documentation comment and
    ///     the attribute list that lead the declaration are untouched — those are leading trivia and sit
    ///     before the span's start. What the span does eat is the keyword's trailing trivia, which is
    ///     why a comment or a directive there withdraws the finding:
    ///     <c>
    /// public /* still virtual in the
    ///     base */ sealed override
    ///     </c> would lose the note under a fix marked safe.
    /// </remarks>
    static void Report(SyntaxNodeAnalysisContext context, SyntaxToken keyword, string message) {
        if (!IsWhitespaceOnly(keyword.TrailingTrivia)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                keyword.GetLocation(),
                FixEdits.Pack((TextSpan.FromBounds(keyword.SpanStart, keyword.GetNextToken().SpanStart), string.Empty)),
                message
            )
        );
    }

    static bool IsWhitespaceOnly(SyntaxNode node) {
        foreach (var trivia in node.DescendantTrivia(descendIntoTrivia: true)) {
            if (!IsWhitespace(trivia)) {
                return false;
            }
        }

        return true;
    }

    static bool IsWhitespaceOnly(SyntaxTriviaList trivia) {
        foreach (var item in trivia) {
            if (!IsWhitespace(item)) {
                return false;
            }
        }

        return true;
    }

    static bool IsWhitespace(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia);
}
