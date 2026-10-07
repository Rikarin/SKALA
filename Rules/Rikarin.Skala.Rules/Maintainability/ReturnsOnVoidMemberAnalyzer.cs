using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     <c>SK7102</c> — a <c>&lt;returns&gt;</c> element on a member that returns nothing.
/// </summary>
/// <remarks>
///     ⚠ <b>The boundary is the <c>void</c> keyword as written</b>, plus the two declarations that have no
///     return type to write at all — a constructor and a finalizer. It is not "returns nothing
///     semantically": an <c>async Task</c> method documenting its task is the .NET convention, and the
///     one hit a looser scan of 5 956 files produced was exactly that shape (#390). A rule that fired
///     there would be wrong on the first real file it met.
///     <para>
///         ⚠ <b>Top-level elements only.</b> A <c>&lt;returns&gt;</c> nested inside <c>&lt;summary&gt;</c>
///         or <c>&lt;code&gt;</c> is not the returns section of anything — no documentation generator
///         reads it as one — and is usually a sample of the tag rather than the tag.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReturnsOnVoidMemberAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ReturnsDocumentedOnVoidMember);

    static readonly ImmutableArray<SyntaxKind> Kinds = ImmutableArray.Create(
        SyntaxKind.MethodDeclaration,
        SyntaxKind.LocalFunctionStatement,
        SyntaxKind.DelegateDeclaration,
        SyntaxKind.OperatorDeclaration,
        SyntaxKind.ConstructorDeclaration,
        SyntaxKind.DestructorDeclaration
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, Kinds);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = context.Node;
        if (Describe(declaration) is not { } what) {
            return;
        }

        var text = declaration.SyntaxTree.GetText(context.CancellationToken);
        foreach (var element in DocumentationElements.TopLevel(declaration, "returns")) {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    element.GetLocation(),
                    FixEdits.Pack((DocumentationElements.DeletionSpan(text, element), string.Empty)),
                    what + " returns nothing, so its `<returns>` documents a value no caller will ever see"
                )
            );
        }
    }

    /// <summary>How the message names the declaration, or <c>null</c> when it returns something.</summary>
    static string? Describe(SyntaxNode declaration) =>
        declaration switch {
            MethodDeclarationSyntax method when IsVoid(method.ReturnType) => "`" + method.Identifier.ValueText + "`",
            LocalFunctionStatementSyntax local when IsVoid(local.ReturnType) =>
                "`" + local.Identifier.ValueText + "`",
            DelegateDeclarationSyntax declared when IsVoid(declared.ReturnType) =>
                "The delegate `" + declared.Identifier.ValueText + "`",
            OperatorDeclarationSyntax op when IsVoid(op.ReturnType) => "The operator `" + op.OperatorToken.Text + "`",
            ConstructorDeclarationSyntax constructor => "The constructor of `" + constructor.Identifier.ValueText + "`",
            DestructorDeclarationSyntax finalizer => "The finalizer of `" + finalizer.Identifier.ValueText + "`",
            _ => null
        };

    /// <summary>
    ///     The keyword, not the type: <c>System.Void</c> cannot be written as a return type, and a
    ///     <c>Task</c> is a value even when its result is not.
    /// </summary>
    static bool IsVoid(TypeSyntax type) =>
        type is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);
}
