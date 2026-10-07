using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2194</c> — a member body assigns a primary constructor parameter, which turns the hidden
///     capture field into mutable state with no declaration.
/// </summary>
/// <remarks>
///     ⚠ <b>Capture itself is the feature and is not reported.</b> A primary constructor parameter
///     read from a member body becomes a compiler-generated field, and that is the point of the
///     syntax — reporting it would be reporting C# 12. What the language gives no way to say is that
///     the field is <c>readonly</c>: the moment any body <em>writes</em> the parameter, the type has
///     mutable instance state that appears in no declaration, has no name a reader can search for,
///     carries no modifier, and is invisible to every rule that reasons about fields.
///     <para>
///         ⚠
///         <b>
///             <c>CS9107</c> was probed and covers a different overlap, and it is not
///             <c>CS9124</c>.
///         </b> The compiler warns — always on, no analyzer package — when a captured
///         parameter's value is <em>also passed to the base constructor</em>, because the base may
///         capture it too. It says nothing about a parameter that is merely assigned. That case is
///         excluded here rather than reported twice.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Records are excluded, and the reason is the trap this repository has already paid
///             for.
///         </b> In a positional record the parameter is also where the property is written down,
///         the two symbols point at the same <c>ParameterSyntax</c>, and a name in a member body
///         resolves to the property rather than to the capture. That is a different analysis with a
///         different answer, and guessing at it from the parameter's shape is how a rule ships dead.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MutableCapturedPrimaryParameterAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor =
        SkalaRule.Descriptor(RuleIds.MutableCapturedPrimaryParameter);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // ⚠ Class and struct declarations only. A record is `RecordDeclarationSyntax`, a different
        // node kind, so the exclusion is structural rather than a test somebody can forget.
        var registrar = PartialConstructorDefinitions.Visiting(context);
        registrar.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (TypeDeclarationSyntax)context.Node;
        if (declaration.ParameterList is not { Parameters.Count: > 0 }
            || !SkalaRule.MeetsLanguageVersion(context.Compilation, "12.0")) {
            return;
        }

        var passedToBase = BaseArguments(declaration);
        foreach (var symbol in PrimaryConstructorWrites.WrittenParameters(
                     declaration,
                     context.SemanticModel,
                     context.CancellationToken
                 )) {
            var declared = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken);
            if (passedToBase.Contains(symbol.Name) || declared is not ParameterSyntax parameter) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    parameter.GetLocation(),
                    "`"
                    + parameter.Identifier.ValueText
                    + "` is captured and assigned, so the type carries a mutable field that is declared nowhere"
                )
            );
        }
    }

    /// <summary>
    ///     ⚠ Matched on the identifier written in the base-constructor argument list, which is what
    ///     <c>CS9107</c> itself keys on. Anything more elaborate would start disagreeing with the
    ///     compiler about the one case it does report.
    /// </summary>
    static ImmutableHashSet<string> BaseArguments(TypeDeclarationSyntax declaration) =>
        declaration.BaseList?.Types
            .OfType<PrimaryConstructorBaseTypeSyntax>()
            .SelectMany(static baseType => baseType.ArgumentList.Arguments)
            .SelectMany(static argument => argument.Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            .Select(static name => name.Identifier.ValueText)
            .ToImmutableHashSet(System.StringComparer.Ordinal)
        ?? ImmutableHashSet<string>.Empty;
}
