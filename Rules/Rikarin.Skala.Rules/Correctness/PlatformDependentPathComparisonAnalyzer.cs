using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2152</c> — a path compared with a hard-coded case-insensitive <c>StringComparison</c>.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "Culture, comparison policy and query shape".
///     <para>
///         Two paths differing only in case are one file on Windows and on a default macOS volume, and
///         two files on Linux. Folding them together on Linux serves one file's answer for another,
///         and it is invisible on the developer's machine — it appears in the container.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Skala already holds a position on this and the rule is written from it rather than
///             from the upstream idea.
///         </b> <c>SarifWriter.PathComparison</c> is
///         <c>OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase</c>,
///         and <c>CacheKeyPathTests</c> asserts the correct answer on all three platforms rather than
///         skipping on two of them. So the guidance is not "stop ignoring case" — it is "compare the
///         path the way this file system compares it", decided at run time.
///     </para>
///     <para>
///         ⚠ <b>An operand must be provably a path, and a name is not a proof.</b> A parameter called
///         <c>filePath</c> does not qualify. Name-based path detection is how this kind of rule
///         acquires its false positives, and the recall it costs is recall on code where the rule
///         would be guessing.
///     </para>
///     <para>
///         ⚠ <b>Only the case-insensitive direction is reported.</b> A hard-coded <c>Ordinal</c> is
///         wrong on Windows for the same reason, but reporting it would fire on every correct
///         comparison written by someone who targets Linux only, and nothing in the source separates
///         the two.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PlatformDependentPathComparisonAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.PlatformDependentPathComparison);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var comparison = start.Compilation.GetTypeByMetadataName("System.StringComparison");
                var path = start.Compilation.GetTypeByMetadataName("System.IO.Path");
                var info = start.Compilation.GetTypeByMetadataName("System.IO.FileSystemInfo");
                if (comparison is null || (path is null && info is null)) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, comparison, path, info),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol comparison,
        INamedTypeSymbol? path,
        INamedTypeSymbol? info
    ) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList is not { } arguments
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method
            || method.ContainingType.SpecialType != SpecialType.System_String) {
            return;
        }

        if (method.Name is not ("Equals" or "StartsWith" or "EndsWith" or "Contains" or "Compare" or "IndexOf")) {
            return;
        }

        var insensitive = CaseInsensitiveArgument(context, arguments, comparison);
        if (insensitive is null) {
            return;
        }

        // Every string this call compares: the receiver, if the call is an instance method, and each
        // argument that is not the policy itself.
        if (!ComparesAPath(context, invocation, arguments, comparison, path, info)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                insensitive.GetLocation(),
                "this compares a path with a hard-coded case-insensitive policy, which is right on "
                + "Windows and macOS and wrong on Linux, where two spellings are two files; select the "
                + "comparison from the platform instead"
            )
        );
    }

    static ExpressionSyntax? CaseInsensitiveArgument(
        SyntaxNodeAnalysisContext context,
        ArgumentListSyntax arguments,
        INamedTypeSymbol comparison
    ) {
        foreach (var argument in arguments.Arguments) {
            if (!SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetTypeInfo(argument.Expression, context.CancellationToken).Type,
                    comparison
                )) {
                continue;
            }

            // ⚠ A constant only. A `StringComparison` reached through a field, property or parameter
            // is already the shape this rule asks for — the value can be chosen at run time — and
            // reporting it would report the fix.
            if (context.SemanticModel.GetSymbolInfo(argument.Expression, context.CancellationToken).Symbol
                is IFieldSymbol { IsConst: true } member
                && member.Name is "OrdinalIgnoreCase"
                or "InvariantCultureIgnoreCase"
                or "CurrentCultureIgnoreCase") {
                return argument.Expression;
            }
        }

        return null;
    }

    static bool ComparesAPath(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ArgumentListSyntax arguments,
        INamedTypeSymbol comparison,
        INamedTypeSymbol? path,
        INamedTypeSymbol? info
    ) {
        if (invocation.Expression is MemberAccessExpressionSyntax access
            && IsPathExpression(context, access.Expression, path, info)) {
            return true;
        }

        foreach (var argument in arguments.Arguments) {
            if (SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetTypeInfo(argument.Expression, context.CancellationToken).Type,
                    comparison
                )) {
                continue;
            }

            if (IsPathExpression(context, argument.Expression, path, info)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ The proof, and the whole of it: the value came out of <c>System.IO.Path</c>, or off a
    ///     <c>FileSystemInfo</c>.
    /// </summary>
    /// <remarks>
    ///     Both are symbol facts rather than spellings, so no naming convention has to hold for the
    ///     rule to be right and no naming convention can make it wrong.
    /// </remarks>
    static bool IsPathExpression(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression,
        INamedTypeSymbol? path,
        INamedTypeSymbol? info
    ) {
        var symbol = context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol;
        if (symbol is null) {
            return false;
        }

        if (path is not null && SymbolEqualityComparer.Default.Equals(symbol.ContainingType, path)) {
            return true;
        }

        if (info is null || symbol.Name is not ("FullName" or "DirectoryName" or "Name")) {
            return false;
        }

        for (var type = symbol.ContainingType; type is not null; type = type.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(type, info)) {
                return true;
            }
        }

        return false;
    }
}
