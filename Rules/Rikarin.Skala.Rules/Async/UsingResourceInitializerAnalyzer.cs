using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3511</c> — the <c>using</c> resource is built with an object initializer, so there is a
///     window in which it exists and nothing owns it.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime".
///     <c>using var x = new Foo { Bar = Baz() }</c> reads as if the <c>using</c> protects the
///     <c>new Foo</c>. It does not: the object initializer runs <em>after</em> the constructor and
///     <em>before</em> the assignment, so if <c>Baz()</c> throws, a constructed <c>Foo</c> — with its
///     handle, its socket, its pooled buffer — is on the floor with nothing holding a reference to it
///     and no <c>Dispose</c> ever reaching it. The leak is invisible in every test where the
///     initializer does not throw, which is all of them.
///     <para>
///         ⚠ The repair is to make the assignment happen where the <c>using</c> can see it: construct
///         first, assign after. It is mechanical, and the rule reports only the shapes where it is.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UsingResourceInitializerAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.UsingResourceObjectInitializer);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var disposable = start.Compilation.GetTypeByMetadataName("System.IDisposable");
                var asyncDisposable = start.Compilation.GetTypeByMetadataName("System.IAsyncDisposable");
                if (disposable is null && asyncDisposable is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, disposable, asyncDisposable),
                    SyntaxKind.UsingStatement,
                    SyntaxKind.LocalDeclarationStatement
                );
            }
        );
    }

    static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? disposable,
        INamedTypeSymbol? asyncDisposable
    ) {
        var owner = (StatementSyntax)context.Node;

        // ⚠ Where the hoisted assignments have to land differs between the two spellings, and the
        // fix is the whole rule, so a shape with nowhere to put them is not reported at all. The
        // `using (expr)` form declares no name to assign through; the statement form needs a block.
        int insertion;
        string indent;
        switch (owner) {
            case LocalDeclarationStatementSyntax { UsingKeyword.RawKind: not (int)SyntaxKind.None } declaration:
                insertion = declaration.Span.End;
                indent = UsingResource.IndentOf(declaration);
                break;

            case UsingStatementSyntax { Statement: BlockSyntax block } use:
                insertion = block.OpenBraceToken.Span.End;
                indent = UsingResource.IndentOf(use) + "    ";
                break;

            default:
                return;
        }

        if (UsingResource.DeclaredVariable(owner) is not { } variable
            || variable.Initializer?.Value is not BaseObjectCreationExpressionSyntax {
                Initializer.RawKind: (int)SyntaxKind.ObjectInitializerExpression
            } creation) {
            return;
        }

        var initializer = creation.Initializer!;

        // ⚠ A comment inside the initializer is data the fix would destroy, and this is not
        // hypothetical: the reference tree's `IrradianceFieldRenderer` initializer carries a
        // six-line note explaining which pass reads the bound names, sitting between two members.
        // The hoist rebuilds the assignments from their expressions and the trivia between them is
        // not part of any expression, so it would go out with the braces — silently, under a fix
        // marked safe.
        if (initializer.Expressions.Count == 0 || ContainsAComment(initializer)) {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is not ILocalSymbol local) {
            return;
        }

        // ⚠ Reference types only. A `using` local is read-only, so `x.Member = value` on a
        // struct-typed one is CS1654 — the hoisted form does not compile and there is no other one.
        if (local.Type is not { IsReferenceType: true } type
            || type.TypeKind == TypeKind.Error
            || !UsingResource.Implements(type, disposable)
            && !UsingResource.Implements(type, asyncDisposable)
            || HasARequiredMember(type)) {
            return;
        }

        var assignments = new List<string>(initializer.Expressions.Count);
        foreach (var expression in initializer.Expressions) {
            // Only `Name = value`. A nested initializer (`A = { B = 1 }`) has no value to assign, an
            // indexed one (`[0] = v`) and a collection element are different rewrites, and none of
            // the three is what this rule was proposed for.
            if (expression is not AssignmentExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                    Left: IdentifierNameSyntax member
                } assignment
                || assignment.Right is InitializerExpressionSyntax) {
                return;
            }

            // ⚠ An `init` accessor is assignable in an object initializer and nowhere else, so
            // hoisting it produces text that parses and does not bind. Accessibility needs no check:
            // the hoisted assignment sits at the same site the initializer did, and the two obey the
            // same rules.
            switch (context.SemanticModel.GetSymbolInfo(member, context.CancellationToken).Symbol) {
                case IPropertySymbol { SetMethod: { IsInitOnly: false } }:
                case IFieldSymbol { IsReadOnly: false, IsConst: false }:
                    break;
                default:
                    return;
            }

            assignments.Add(local.Name + "." + member.Identifier.ValueText + " = " + assignment.Right.ToString() + ";");
        }

        var hoisted = new StringBuilder();
        foreach (var assignment in assignments) {
            hoisted.Append('\n').Append(indent).Append(assignment);
        }

        // ⚠ `new Foo { … }` carries no argument list, and `new Foo` on its own is not an
        // expression — so removing the initializer there has to put the parentheses back. An
        // implicit `new() { … }` always has one, which is why this only ever sees the explicit form.
        var removal = TextSpan.FromBounds(
            creation is ObjectCreationExpressionSyntax { ArgumentList: null } bare
                ? bare.Type.Span.End
                : creation.ArgumentList!.Span.End,
            initializer.Span.End
        );

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                initializer.GetLocation(),
                FixEdits.Pack(
                    (removal, creation.ArgumentList is null ? "()" : string.Empty),
                    (new TextSpan(insertion, 0), hoisted.ToString())
                ),
                "`"
                + type.Name
                + "` is constructed before the `using` owns it; an initializer that throws leaks it"
            )
        );
    }

    static bool ContainsAComment(SyntaxNode node) {
        foreach (var trivia in node.DescendantTrivia()) {
            switch ((SyntaxKind)trivia.RawKind) {
                case SyntaxKind.SingleLineCommentTrivia:
                case SyntaxKind.MultiLineCommentTrivia:
                case SyntaxKind.SingleLineDocumentationCommentTrivia:
                case SyntaxKind.MultiLineDocumentationCommentTrivia:
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ A <c>required</c> member makes <c>new Foo()</c> alone a compile error (CS9035).
    /// </summary>
    /// <remarks>
    ///     The object initializer is what satisfies the requirement, so hoisting it out is exactly
    ///     what the language forbids. The shape is genuinely unfixable rather than merely awkward, and
    ///     an unfixable finding on a rule that declares a fix is a finding <c>skala fix</c> cannot
    ///     honour.
    /// </remarks>
    static bool HasARequiredMember(ITypeSymbol type) {
        for (var current = type; current is not null; current = current.BaseType) {
            foreach (var member in current.GetMembers()) {
                if (member is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true }) {
                    return true;
                }
            }
        }

        return false;
    }
}
