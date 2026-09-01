using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Design;

/// <summary>
///     <c>SK6032</c> — an <c>abstract</c> class with nothing to override, nothing <c>protected</c> and
///     no base.
/// </summary>
/// <remarks>
///     <c>abstract</c> on a class is two statements at once: "do not instantiate this" and "a derived
///     type completes it". A class that declares no <c>abstract</c> and no <c>virtual</c> member has
///     nothing for a derived type to complete; if it also declares nothing <c>protected</c> and derives
///     from nothing, then no part of it is arranged for derivation at all, and the keyword is doing
///     only the first half of its job — for a reason the declaration does not give.
///     <para>
///         ⚠ <b>The <c>protected</c> exemption is the one carrying the rule, and a protected constructor
///         is the usual form of it.</b> A base class that shares state through a protected constructor,
///         a protected field or a protected helper is set up for derivation whether or not it declares
///         anything abstract, and reporting it would be reporting the most ordinary base class in C#.
///         What is left after that exemption is a class with public and private concrete members, no
///         base, no hooks and no derived-only surface — where <c>abstract</c> prevents instantiation and
///         does nothing else.
///     </para>
///     <para>
///         ⚠ Syntactic, against the proposal's <c>Semantic</c>. The base-list exemption is what makes it
///         so: an <c>abstract</c> class with a base list may inherit unimplemented abstract members or
///         carry unimplemented interface members, and both of those are real things to implement that
///         live outside this declaration. Refusing to report any of them removes the only question that
///         needed a symbol.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AbstractTypeWithoutAbstractionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.AbstractTypeWithoutAbstraction);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // ⚠ `class` only, never `record`. An `abstract record` is overwhelmingly the root of a closed
        // hierarchy, and its generated members — the copy constructor, `EqualsContract`, the printing
        // members — are the derivation surface a hand-written class has to declare. There is no
        // shape there that this rule could read as an omission.
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (ClassDeclarationSyntax)context.Node;

        var isAbstract = false;
        foreach (var modifier in declaration.Modifiers) {
            if (modifier.IsKind(SyntaxKind.AbstractKeyword)) {
                isAbstract = true;
            }

            // The other part may declare the abstract member, or may not exist yet because a
            // generator writes it. The same exemption keeps SK6023 out of generated codebases.
            if (modifier.IsKind(SyntaxKind.PartialKeyword)) {
                return;
            }
        }

        if (!isAbstract) {
            return;
        }

        // A base list is an exemption on its own: the type may be inheriting abstract members it does
        // not implement, or carrying interface members a derived type must supply. Both are real
        // things to implement, and neither is visible from this declaration.
        if (declaration.BaseList is not null) {
            return;
        }

        // Something outside this file reads an attributed type — a framework that requires a base
        // class, a generator, a serializer — so the shape of the declaration is not the whole story.
        if (declaration.AttributeLists.Count > 0) {
            return;
        }

        // ⚠ An `abstract class Foo { }` with no members at all is SK6023's finding, and reporting it
        // here as well would bill one declaration twice for the same omission.
        if (declaration.Members.Count == 0) {
            return;
        }

        foreach (var member in declaration.Members) {
            if (HasDerivationSurface(member.Modifiers)) {
                return;
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declaration.Identifier.GetLocation(),
                "`"
                + declaration.Identifier.ValueText
                + "` is `abstract` and declares nothing to override, nothing `protected` and no base, "
                + "so `abstract` only prevents instantiation; a concrete class, a `static` class or an "
                + "interface was meant"
            )
        );
    }

    /// <summary>Whether a member is arranged for a derived type to use or to complete.</summary>
    /// <remarks>
    ///     ⚠ <c>virtual</c> counts as much as <c>abstract</c>. "You may override this" is an extension
    ///     point the author put there deliberately, and a visitor base whose every method is an empty
    ///     <c>virtual</c> is the canonical shape of one.
    /// </remarks>
    static bool HasDerivationSurface(SyntaxTokenList modifiers) {
        foreach (var modifier in modifiers) {
            switch ((SyntaxKind)modifier.RawKind) {
                case SyntaxKind.AbstractKeyword:
                case SyntaxKind.VirtualKeyword:
                case SyntaxKind.ProtectedKeyword:
                    return true;
            }
        }

        return false;
    }
}
