using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Design;

/// <summary>
///     <c>SK6034</c> — an externally visible <c>const</c>, copied into every caller at compile time.
/// </summary>
/// <remarks>
///     ⚠ <b>This rule is about a distribution property, not a syntax property.</b> The compiler does not
///     emit a field read for <c>Limits.MaxRetries</c>; it emits the literal <c>3</c>, into the consumer's
///     assembly. Ship a new version of the library with the value changed and every assembly built
///     against the old one keeps the old number — no error, no warning, no binding failure, and nothing
///     in either build says the two disagree. <c>static readonly</c> is read at run time and does not
///     have this property.
///     <para>
///         The rule is semantic because the question is whether the field escapes the assembly, and that
///         is the field's accessibility <em>and</em> every containing type's, which is a walk over
///         symbols rather than over one declaration's modifiers.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PublicConstantAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.PublicConstantField);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var field = (FieldDeclarationSyntax)context.Node;

        var keyword = default(SyntaxToken);
        foreach (var modifier in field.Modifiers) {
            if (modifier.IsKind(SyntaxKind.ConstKeyword)) {
                keyword = modifier;
            }
        }

        if (!keyword.IsKind(SyntaxKind.ConstKeyword)) {
            return;
        }

        // ⚠ The fix replaces this one token. A preprocessor directive in its trivia means the token
        // the fix names may not be the token every branch compiles — the same guard SK6003 uses.
        if (keyword.ContainsDirectives) {
            return;
        }

        var declarator = field.Declaration.Variables.Count > 0 ? field.Declaration.Variables[0] : default;
        if (declarator is null) {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken) is not IFieldSymbol symbol) {
            return;
        }

        // ⚠ An interface's constants are excluded. A `static readonly` field in an interface is legal
        // and is not the same declaration — it changes what implementers see and what the runtime
        // initializes — so the one-token fix would not be the repair it claims to be.
        if (symbol.ContainingType is { TypeKind: TypeKind.Interface }) {
            return;
        }

        if (!IsExternallyVisible(symbol)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                keyword.GetLocation(),
                FixEdits.Pack((keyword.Span, "static readonly")),
                "`"
                + declarator.Identifier.ValueText
                + "` is visible outside this assembly and `const`, so its value is copied into every "
                + "caller at compile time; shipping a new value leaves every caller that is not "
                + "rebuilt on the old one, with no error anywhere"
            )
        );
    }

    /// <summary>
    ///     Whether the symbol and every type containing it are visible outside the assembly.
    /// </summary>
    /// <remarks>
    ///     ⚠ The field's own accessibility is not enough. A <c>public const</c> inside an
    ///     <c>internal</c> class never leaves the assembly, so the value is never copied anywhere that
    ///     is compiled separately and there is nothing to report. Walking the containing chain is the
    ///     whole difference between this rule and one that reports every <c>public const</c> in a
    ///     program.
    /// </remarks>
    static bool IsExternallyVisible(ISymbol symbol) {
        for (var current = symbol; current is not null and not INamespaceSymbol; current = current.ContainingSymbol) {
            switch (current.DeclaredAccessibility) {
                case Accessibility.Public:
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                    continue;

                default:
                    return false;
            }
        }

        return true;
    }
}
