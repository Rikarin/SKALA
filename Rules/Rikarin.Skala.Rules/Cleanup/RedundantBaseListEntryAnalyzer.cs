using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Cleanup;

/// <summary><c>SK0280</c> — a base list naming an interface another entry already brings in.</summary>
/// <remarks>
///     <para>
///         <c>class C : IList&lt;T&gt;, IEnumerable&lt;T&gt;</c> — the second entry is in the first
///         entry's own interface set, so the declaration says the same thing twice and a reader has to
///         work out which of the two is the one that matters.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Only an interface implied by another <em>interface</em> in the same list, never one
///             implied by the base class
///         </b>, and that is a correctness constraint rather than a scope cut.
///         ReSharper reports both — measured: <c>class D : B, IBase</c> where <c>B : IBase</c> is
///         flagged by <c>jb inspectcode</c> 2025.2.6 — and the two are not the same edit. Re-listing an
///         interface in a derived class makes the derived class <em>re-implement</em> it, so the
///         interface mapping is recomputed there: a member declared <c>new</c> in the derived class
///         becomes the implementation instead of the base's. Deleting that entry silently changes which
///         method an interface call reaches. When one interface entry implies another there is no such
///         hazard — the type has to implement the derived interface either way, and the mapping is
///         computed in the same place with or without the redundant name.
///     </para>
///     <para>
///         ⚠ <b><c>class C : object</c> is not this finding and is not reported.</b> It was probed
///         alongside the rest and ReSharper does not report it either; the concept here is a duplicate
///         interface, and the explicit <c>object</c> base is a different sentence with a different
///         (empty) audience.
///     </para>
///     <para>
///         ⚠ <b>A default interface member withdraws the finding.</b> Where any interface in the
///         closure declares a member with a body, or re-abstracts one it inherited, the most-specific
///         implementation is decided by the interface set as written, and dropping a name from the base
///         list is a question about method resolution rather than about duplication. Rather than answer
///         it, the rule declines.
///     </para>
///     <para>
///         ⚠ <b>Explicit implementation does not withdraw it.</b> <c>void IBase.A()</c> needs
///         <c>IBase</c> in the type's interface set, which it still is through the derived interface —
///         so the code still compiles and still means the same thing. That was the shape most likely to
///         be got wrong here, so it has a fixture of its own.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantBaseListEntryAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RedundantBaseListEntry);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (TypeDeclarationSyntax)context.Node;
        if (declaration.BaseList is not { Types.Count: > 1 } baseList) {
            return;
        }

        // ⚠ COM demands the declared order: the interface list is the vtable layout, so an entry that
        // is redundant to C# is load-bearing to the marshaller.
        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } declared
            || declared.IsComImport) {
            return;
        }

        var written = new List<(BaseTypeSyntax Node, INamedTypeSymbol Symbol)>();
        foreach (var entry in baseList.Types) {
            if (context.SemanticModel.GetSymbolInfo(entry.Type, context.CancellationToken).Symbol
                is INamedTypeSymbol { TypeKind: TypeKind.Interface } symbol) {
                written.Add((entry, symbol));
            }
        }

        if (written.Count < 2) {
            return;
        }

        for (var index = 0; index < written.Count; index++) {
            var (node, symbol) = written[index];
            if (!ImpliedByAnother(written, index, symbol) || CarriesAnImplementation(symbol)) {
                continue;
            }

            var span = DeletedSpan(baseList, node);
            if (span is null
                || RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(context.Node.SyntaxTree, span.Value)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    node.GetLocation(),
                    FixEdits.Pack((span.Value, string.Empty)),
                    $"`{symbol.Name}` is already in the interface set through another entry in this base list"
                )
            );

            // ⚠ One finding per declaration. `class C : IA, IB, IC` where IA implies both would emit two
            // deletions whose spans are disjoint but whose *commas* are not independent — applying both
            // to `: IA, IB, IC` is fine, but applying both to `: IB, IC, IA` leaves `: IA` with a
            // leading comma. One at a time converges; the next pass sees the rest.
            return;
        }
    }

    static bool ImpliedByAnother(
        List<(BaseTypeSyntax Node, INamedTypeSymbol Symbol)> written,
        int index,
        INamedTypeSymbol symbol
    ) {
        for (var other = 0; other < written.Count; other++) {
            if (other == index) {
                continue;
            }

            foreach (var inherited in written[other].Symbol.AllInterfaces) {
                if (SymbolEqualityComparer.Default.Equals(inherited, symbol)) {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ Whether anything in the interface's closure has a body, which makes the interface set an
    ///     answer to "which implementation wins" rather than a list of contracts.
    /// </summary>
    static bool CarriesAnImplementation(INamedTypeSymbol symbol) {
        if (HasBody(symbol)) {
            return true;
        }

        foreach (var inherited in symbol.AllInterfaces) {
            if (HasBody(inherited)) {
                return true;
            }
        }

        return false;
    }

    static bool HasBody(INamedTypeSymbol symbol) {
        foreach (var member in symbol.GetMembers()) {
            if (member.IsStatic || member.IsImplicitlyDeclared) {
                continue;
            }

            // A member with no body is the ordinary interface contract; one with a body is a default
            // implementation, and one that is abstract *and* explicit is a re-abstraction. Either makes
            // the written interface set part of the resolution.
            if (!member.IsAbstract) {
                return true;
            }

            if (member is IMethodSymbol { ExplicitInterfaceImplementations.IsEmpty: false }) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     The entry and the comma that binds it to its neighbour.
    /// </summary>
    /// <remarks>
    ///     ⚠ Which comma depends on where the entry sits. For anything but the first entry the span
    ///     runs back to the <em>previous</em> entry's end, so the separator in front goes with it; for
    ///     the first it runs forward to the next entry's start. Deleting only the entry leaves
    ///     <c>: , IB</c>, which does not parse — and a fix whose output does not parse is worse than no
    ///     fix, because <c>SK9099</c> then refuses to write the file at all.
    /// </remarks>
    static TextSpan? DeletedSpan(BaseListSyntax baseList, BaseTypeSyntax node) {
        var index = baseList.Types.IndexOf(node);
        if (index < 0) {
            return null;
        }

        return index > 0
            ? TextSpan.FromBounds(baseList.Types[index - 1].Span.End, node.Span.End)
            : TextSpan.FromBounds(node.SpanStart, baseList.Types[1].SpanStart);
    }
}
