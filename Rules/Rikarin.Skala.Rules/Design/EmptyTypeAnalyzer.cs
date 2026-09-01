using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Design;

/// <summary><c>SK6023</c> — a type with no members, no base, no attributes and no type parameters.</summary>
/// <remarks>
///     ⚠ The exemptions are the rule. A detector for "no members" is three lines; deciding which empty
///     types are deliberate is the whole of this one — and every answer here is decidable from the
///     declaration alone, which is why the rule is syntactic.
///     <para>
///         An empty type is usually a marker attribute, a marker interface, a named specialisation of a
///         generic base, or the leftovers of an edit. The first three are legitimate and each one is
///         visible in the declaration: an attribute carries a base list or an attribute list, a marker
///         interface is an interface, a specialisation has a base list. What is left has nothing
///         attached in any direction — a name and a pair of braces.
///     </para>
///     <para>
///         ⚠ The one shape that stays genuinely ambiguous is a non-generic phantom type:
///         <c>sealed class Metres { }</c> used only as a type argument. It is reported and it is a false
///         positive. Recognising it needs every use of the type, which is a whole-compilation question,
///         and the rule ships at <c>suggestion</c> because of it.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptyTypeAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.EmptyType);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // ⚠ No InterfaceDeclaration. An empty interface is a marker, which is the *correct* shape for
        // one — the only kind a type can adopt without changing what it inherits from — so the whole
        // kind is out of scope rather than filtered later. Enums and delegates likewise.
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (TypeDeclarationSyntax)context.Node;
        if (declaration.Members.Count > 0) {
            return;
        }

        // A base list carries the declaration's whole meaning: a closed generic given a name, a leaf
        // of a closed hierarchy, an interface implementation. It also covers every attribute type,
        // which is why marker attributes need no test of their own.
        if (declaration.BaseList is not null) {
            return;
        }

        // Something outside this file reads an attributed type — a generator, a framework, a
        // serializer — so the empty body is not evidence that nothing uses it.
        if (declaration.AttributeLists.Count > 0) {
            return;
        }

        // ⚠ The exemption that keeps the rule out of every source-generated codebase: the other part
        // may be in another file, or may not exist yet because a generator writes it.
        foreach (var modifier in declaration.Modifiers) {
            if (modifier.IsKind(SyntaxKind.PartialKeyword)) {
                return;
            }
        }

        // A type parameter says the type exists to be *named* rather than instantiated, which is what
        // a phantom or a generic marker is.
        if (declaration.TypeParameterList is not null) {
            return;
        }

        if (declaration is RecordDeclarationSyntax record) {
            // `record Foo(int X);` declares members through its parameter list.
            if (record.ParameterList is { Parameters.Count: > 0 }) {
                return;
            }

            // ⚠ `record Foo;` is never reported and `record Foo { }` is. The semicolon form exists
            // precisely to declare a complete type with nothing in it — a unit, a message — so
            // writing one is a statement. Braces around nothing are an omission.
            if (record.OpenBraceToken.IsKind(SyntaxKind.None)) {
                return;
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declaration.Identifier.GetLocation(),
                "`"
                + declaration.Identifier.ValueText
                + "` declares no members, derives from nothing and carries no attribute, so nothing can "
                + "use it as a marker; finish it or remove it"
            )
        );
    }
}
