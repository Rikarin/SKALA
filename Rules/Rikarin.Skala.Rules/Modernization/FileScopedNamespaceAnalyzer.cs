using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
/// <c>SK1005</c> — a file with exactly one block-scoped namespace and nothing beside it.
/// </summary>
/// <remarks>
/// docs/plan/08-rule-catalogue.md § "Language shape". Syntactic, so it runs under
/// <c>--load=loose</c>, which is the mode an agent's scratch file is analysed in.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FileScopedNamespaceAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.FileScopedNamespace);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.FileScopedNamespace);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.NamespaceDeclaration);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (NamespaceDeclarationSyntax)context.Node;

        // The namespace must be the compilation unit's only member. Two namespaces in one file
        // cannot be converted at all, and a type beside the namespace would move into it.
        if (declaration.Parent is not CompilationUnitSyntax unit
            || unit.Members.Count != 1
            || !ReferenceEquals(unit.Members[0], declaration)) {
            return;
        }

        // A nested namespace has to keep its braces, so the outer one has to keep its own.
        foreach (var member in declaration.Members) {
            if (member is BaseNamespaceDeclarationSyntax) {
                return;
            }
        }

        // ⚠ The conversion deletes two braces. If a preprocessor directive lives anywhere between
        // them, one branch may own a brace the other does not and the deletion is a guess. This is
        // the same reason PreprocessorGuard exists on the formatter side, for the same construct.
        if (declaration.ContainsDirectives) {
            return;
        }

        // The closing brace's leading trivia is deleted with it, so it has to be nothing but
        // whitespace — a comment sitting above the brace is content, not layout.
        foreach (var trivia in declaration.CloseBraceToken.LeadingTrivia) {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia)) {
                return;
            }
        }

        // Likewise the text between the name and the open brace: `namespace X /* why */ {`.
        foreach (var trivia in declaration.OpenBraceToken.LeadingTrivia) {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia)) {
                return;
            }
        }

        var fix = FixEdits.Pack(
            (TextSpan.FromBounds(declaration.Name.Span.End, declaration.OpenBraceToken.Span.End), ";"),
            (
                TextSpan.FromBounds(
                    declaration.CloseBraceToken.FullSpan.Start,
                    declaration.CloseBraceToken.Span.End
                ),
                string.Empty
            )
        );

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(
                    declaration.SyntaxTree,
                    TextSpan.FromBounds(
                        declaration.NamespaceKeyword.SpanStart,
                        declaration.Name.Span.End
                    )
                ),
                fix,
                "Use a file-scoped namespace: `namespace " + declaration.Name + ";`"
            )
        );
    }
}
