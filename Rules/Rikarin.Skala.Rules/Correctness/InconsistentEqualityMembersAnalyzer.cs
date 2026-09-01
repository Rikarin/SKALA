using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary><c>SK2044</c> — the equality members are inconsistent with each other.</summary>
/// <remarks>
///     ⚠ <b>One finding per type, and <c>SK2004</c>'s span is left alone by construction.</b> The
///     two inconsistencies below are aspects of a single omission, so the first that matches is
///     reported and the other is not; and a type <c>SK2004</c> already reports — implementing
///     <c>IEquatable&lt;Self&gt;</c> with no <c>Equals(object)</c> — is refused before either is
///     asked, so no severity setting can turn the pair into a duplicate.
///     <para>
///         ⚠ A type whose base list did not bind is refused before anything else. See
///         <see cref="EqualityMembers.BindsCompletely" />: the measurement on the reference trees
///         found this rule reporting a type for not implementing the very interface it declares,
///         because the name had bound to an error type.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InconsistentEqualityMembersAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.InconsistentEqualityMembers);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
            is not INamedTypeSymbol { IsRecord: false } type
            || type.DeclaringSyntaxReferences.FirstOrDefault() is not { } first
            || first.SyntaxTree != declaration.SyntaxTree
            || first.Span != declaration.Span
            || !EqualityMembers.BindsCompletely(type)
            || EqualityMembers.IsReportedByIncompleteEqualityContract(type, context.Compilation)) {
            return;
        }

        if (Message(type, context.Compilation) is { } message) {
            context.ReportDiagnostic(Diagnostic.Create(Descriptor, declaration.Identifier.GetLocation(), message));
        }
    }

    /// <summary>
    ///     ⚠ <b>Two inconsistencies, not the three the issue proposed.</b>
    /// </summary>
    /// <remarks>
    ///     "`operator ==` with no `Equals(object)` override" was built and then withdrawn: a probe
    ///     compiled against a real project reports it as <c>CS0660</c> and <c>CS0661</c>, which are
    ///     compiler warnings, always on, needing no analyzer package and no configuration. Restating
    ///     a diagnostic the compiler already emits is the double-count doc 17 excludes on sight.
    ///     <c>CA1036</c> was checked the same way for the ordering half and does <em>not</em> fire at
    ///     the SDK's recommended level, so that one stands.
    /// </remarks>
    static string? Message(INamedTypeSymbol type, Compilation compilation) =>
        TypedEqualsWithoutContract(type, compilation)
        ?? (EqualityMembers.Operator(type, "op_Equality") is null
            ? null
            : OrderedWithoutRelationalOperators(type, compilation));

    static string? TypedEqualsWithoutContract(INamedTypeSymbol type, Compilation compilation) =>
        EqualityMembers.TypedEquals(type).Any() && !EqualityMembers.ImplementsEquatable(type, compilation)
            ? "`"
            + type.Name
            + "` declares Equals("
            + type.Name
            + ") and does not implement IEquatable<"
            + type.Name
            + ">, so generic comparers never find it"
            : null;

    /// <summary>
    ///     ⚠ Only a type that already declares <c>==</c>. The many types that implement
    ///     <c>IComparable&lt;T&gt;</c> so a <c>Sort</c> works, and want no operators at all, are not
    ///     inconsistent with anything.
    /// </summary>
    static string? OrderedWithoutRelationalOperators(INamedTypeSymbol type, Compilation compilation) {
        if (!Orders(type, compilation)) {
            return null;
        }

        var missing = new[] { "op_LessThan", "op_GreaterThan", "op_LessThanOrEqual", "op_GreaterThanOrEqual" }
            .Where(name => EqualityMembers.Operator(type, name) is null)
            .ToArray();

        return missing.Length == 0
            ? null
            : "`"
            + type.Name
            + "` orders its instances and declares `==` without the relational operators, so half the "
            + "comparison set is missing";
    }

    static bool Orders(INamedTypeSymbol type, Compilation compilation) {
        var generic = compilation.GetTypeByMetadataName("System.IComparable`1");
        var plain = compilation.GetTypeByMetadataName("System.IComparable");
        return type.AllInterfaces.Any(contract =>
            (generic is not null
                && SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, generic)
                && SymbolEqualityComparer.Default.Equals(contract.TypeArguments[0], type))
            || (plain is not null && SymbolEqualityComparer.Default.Equals(contract, plain))
        );
    }
}
