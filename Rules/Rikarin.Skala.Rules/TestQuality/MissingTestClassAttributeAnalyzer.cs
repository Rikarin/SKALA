using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.TestQuality;

/// <summary>
///     <c>SK8020</c> — MSTest `[TestMethod]` members in a class MSTest will never open.
/// </summary>
/// <remarks>
///     ⚠ The discoverer enumerates types carrying <c>[TestClass]</c> and only then reads their methods, so
///     a class without it contributes zero tests and reports nothing at all: not a skip, not a warning, not
///     a line in the summary. A suite that was never run and a suite that passed are the same colour, which
///     is why this ships at <c>warning</c> rather than at the <c>suggestion</c> its noise level would
///     otherwise justify.
///     <para>
///         ⚠ MSTest only, and <see cref="TestFrameworks" /> says why: xUnit has no class attribute to be
///         missing and NUnit made <c>[TestFixture]</c> optional in NUnit 3. The rule would be reporting a
///         convention on those two, not a defect.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingTestClassAttributeAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.TestClassAttributeMissing);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var frameworks = TestFrameworks.Resolve(start.Compilation);

                // A repository with no MSTest reference cannot hold this defect, and resolving the
                // two attributes is the whole of deciding that.
                if (frameworks.MsTestMethodAttribute is null || frameworks.MsTestClassAttribute is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, frameworks),
                    SyntaxKind.ClassDeclaration
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, TestFrameworks frameworks) {
        var declaration = (ClassDeclarationSyntax)context.Node;

        // ⚠ An abstract class holding `[TestMethod]` members is the shared-base pattern MSTest
        // supports: the attribute belongs on the concrete class that inherits them, and putting it
        // here would ask MSTest to instantiate a type it cannot. A static class cannot be a test
        // class at all.
        if (declaration.Modifiers.Any(SyntaxKind.AbstractKeyword)
            || declaration.Modifiers.Any(SyntaxKind.StaticKeyword)
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } type
            || !TestFrameworks.IsAnchorDeclaration(type, declaration)) {
            return;
        }

        for (var current = type; current is not null; current = current.BaseType) {
            if (TestFrameworks.Carries(current, frameworks.MsTestClassAttribute)) {
                return;
            }
        }

        var marker = type.GetMembers()
            .OfType<IMethodSymbol>()
            .SelectMany(static method => method.GetAttributes())
            .FirstOrDefault(attribute => attribute.AttributeClass is { } written
                && Derives(written, frameworks.MsTestMethodAttribute!)
            );
        if (marker is null) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declaration.Identifier.GetLocation(),
                Insertion(declaration, marker, context.CancellationToken),
                "`"
                + type.Name
                + "` declares MSTest test methods and carries no `[TestClass]`, so none of them is discovered"
            )
        );
    }

    static bool Derives(INamedTypeSymbol attribute, INamedTypeSymbol root) {
        for (var current = attribute; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current, root)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     The one edit: an attribute list on its own line above the declaration, indented as the
    ///     declaration is.
    /// </summary>
    /// <remarks>
    ///     ⚠ It is inserted at the declaration's first token rather than before its leading trivia, so a
    ///     documentation comment and the attribute lists already present stay above it and nothing the
    ///     author wrote is displaced. The newline is taken from the file rather than from the environment,
    ///     because a fix is a text edit against the original <c>SourceText</c> (ADR-005) and a CRLF file
    ///     edited with an LF is a file whose diff is every line.
    /// </remarks>
    static ImmutableDictionary<string, string?> Insertion(
        ClassDeclarationSyntax declaration,
        AttributeData marker,
        CancellationToken cancellation
    ) {
        var token = declaration.GetFirstToken();
        var text = declaration.SyntaxTree.GetText(cancellation);
        var line = text.Lines.GetLineFromPosition(token.SpanStart);
        var newline = text.ToString(TextSpan.FromBounds(line.End, line.EndIncludingLineBreak));

        // ⚠ Only whitespace counts as indentation. `namespace X { class C { } }` on one line puts
        // real code between the line start and the token, and copying it into the insertion would
        // duplicate a namespace declaration.
        var prefix = text.ToString(TextSpan.FromBounds(line.Start, token.SpanStart));
        var indent = prefix.All(char.IsWhiteSpace) ? prefix : string.Empty;

        var name = marker.ApplicationSyntaxReference?.GetSyntax(cancellation) is AttributeSyntax syntax
            ? TestFrameworks.Qualify(syntax.Name, "TestClass")
            : "TestClass";

        return FixEdits.Pack(
            (
                new TextSpan(token.SpanStart, 0),
                "[" + name + "]" + (newline.Length == 0 ? "\n" : newline) + indent
            )
        );
    }
}
