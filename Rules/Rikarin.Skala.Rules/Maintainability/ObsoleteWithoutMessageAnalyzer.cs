using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     <c>SK7070</c> — <c>[Obsolete]</c> applied with no message, or with a placeholder message.
/// </summary>
/// <remarks>
///     ⚠ Bound rather than name-matched, for the reason <c>SK7051</c> gives: <c>ObsoleteAttribute</c> is
///     a short name anybody may define, and a rule that matches on spelling reports other people's
///     types. The attribute's only positional parameter is the message, so "no arguments" and "an empty
///     first argument" are the same omission seen twice.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ObsoleteWithoutMessageAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ObsoleteWithoutMessage);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var obsolete = start.Compilation.GetTypeByMetadataName("System.ObsoleteAttribute");
                if (obsolete is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(context => Analyze(context, obsolete), SyntaxKind.Attribute);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol obsolete) {
        var attribute = (AttributeSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol
            is not IMethodSymbol constructor
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, obsolete)) {
            return;
        }

        var message = Message(attribute, constructor);
        if (message is null) {
            Report(context, attribute, "`Obsolete` names no replacement: it has no message");
            return;
        }

        // ⚠ A non-constant message — a `nameof`, a resource lookup, a `const` the model cannot fold —
        // is accepted without inspection. The rule proves an omission; it does not guess at an
        // expression it could not evaluate.
        if (context.SemanticModel.GetConstantValue(message.Expression, context.CancellationToken)
            is { HasValue: true } constant
            && (constant.Value is not string text || !Justification.Meaningful(text))) {
            Report(context, attribute, "`Obsolete` names no replacement: its message is empty or a placeholder");
        }
    }

    /// <summary>The argument bound to the constructor's <c>message</c> parameter, however it is spelled.</summary>
    /// <remarks>
    ///     ⚠ Not "the first positional argument". <c>[Obsolete(message: "…")]</c> is a named-colon
    ///     argument and reads as a property assignment to a syntactic test — a fixture caught exactly
    ///     that false positive. Positions are resolved against the constructor's parameter list, so the
    ///     three spellings the language allows are one answer. <c>NameEquals</c> arguments are property
    ///     initialisers: <c>DiagnosticId</c> and <c>UrlFormat</c> are named that way and neither reaches
    ///     the compiler's warning text, so neither substitutes for the message.
    /// </remarks>
    static AttributeArgumentSyntax? Message(AttributeSyntax attribute, IMethodSymbol constructor) {
        var position = 0;
        foreach (var argument in attribute.ArgumentList?.Arguments ?? default) {
            if (argument.NameEquals is not null) {
                continue;
            }

            if (argument.NameColon is { } named) {
                if (named.Name.Identifier.ValueText == "message") {
                    return argument;
                }

                continue;
            }

            if (position < constructor.Parameters.Length && constructor.Parameters[position].Name == "message") {
                return argument;
            }

            position++;
        }

        return null;
    }

    static void Report(SyntaxNodeAnalysisContext context, AttributeSyntax attribute, string message) =>
        context.ReportDiagnostic(Diagnostic.Create(Descriptor, attribute.Name.GetLocation(), message));
}
