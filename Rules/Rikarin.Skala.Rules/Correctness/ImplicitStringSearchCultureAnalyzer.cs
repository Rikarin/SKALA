using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary><c>SK2150</c> — string searches whose culture policy is implicit.</summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "Culture, comparison policy and query shape".
///     <para>
///         ⚠
///         <b>
///             This is the half <c>SK2010</c> does not do, and the difference is not the method
///             list.
///         </b> <c>SK2010</c> reports a comparison, whose culture-dependence surfaces as an
///         answer of the wrong <em>truth value</em> — visibly wrong, at the site that asked. A search
///         returns an <em>offset</em>. A <c>LastIndexOf("-")</c> that lands one character out on a
///         Turkish machine feeds a <c>Substring</c>, and what arrives at the reader is a truncated
///         identifier several frames away with nothing culture-shaped about it.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The method table is the framework's documented behaviour, and most of
///             <see cref="string" /> is deliberately missing from it.
///         </b> <c>Contains(string)</c>,
///         <c>IndexOf(char)</c>, <c>LastIndexOf(char)</c>, <c>StartsWith(char)</c> and
///         <c>EndsWith(char)</c> are <em>already ordinal</em> on .NET. Reporting them would be advising
///         the author to write down the behaviour they already have, which is the shape of finding that
///         teaches a reader to stop reading the rule's findings. Only the four names whose
///         <c>string</c> overload documents current-culture semantics are inspected, and only when the
///         first argument really is a <c>string</c>.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ImplicitStringSearchCultureAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ImplicitStringSearchCulture);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList is not { } arguments
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method
            || method.IsStatic
            || method.ContainingType.SpecialType != SpecialType.System_String) {
            return;
        }

        // ⚠ The four names, and the `string` first parameter that separates the culture-sensitive
        // overload from the ordinal one that shares its name.
        if (method.Name is not ("IndexOf" or "LastIndexOf" or "StartsWith" or "EndsWith")
            || method.Parameters.Length == 0
            || method.Parameters[0].Type.SpecialType != SpecialType.System_String) {
            return;
        }

        if (method.Parameters.Any(parameter => IsPolicyParameter(parameter.Type, context.Compilation))) {
            return;
        }

        var insertion = new TextSpan(arguments.CloseParenToken.SpanStart, 0);
        var fix = FixEdits.Pack((insertion, ", " + ComparisonName(context, invocation) + ".Ordinal"));

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                fix,
                "`"
                + method.Name
                + "` with no StringComparison searches using the ambient culture, so the offset it "
                + "returns depends on a process setting nothing here mentions; pass "
                + "StringComparison.Ordinal"
            )
        );
    }

    static bool IsPolicyParameter(ITypeSymbol type, Compilation compilation) =>
        SymbolEqualityComparer.Default.Equals(type, compilation.GetTypeByMetadataName("System.StringComparison"))
        || SymbolEqualityComparer.Default.Equals(
            type,
            compilation.GetTypeByMetadataName("System.Globalization.CultureInfo")
        );

    /// <summary>
    ///     ⚠ The name to write, decided by asking whether it binds rather than by assuming it does.
    /// </summary>
    /// <remarks>
    ///     A file with no <c>using System;</c> and no implicit usings is ordinary in generated code and
    ///     in anything targeting an older SDK, and an edit emitting a bare <c>StringComparison</c> there
    ///     is a fix that turns a warning into <c>CS0103</c>. Looking the name up at the call site costs
    ///     one semantic query and removes the whole class of failure;
    ///     <c>EveryFix_SilencesTheRuleAndIntroducesNoDiagnostic</c> re-binds the fixed text and is what
    ///     would otherwise catch it, one fixture at a time.
    /// </remarks>
    static string ComparisonName(SyntaxNodeAnalysisContext context, SyntaxNode node) {
        var comparison = context.Compilation.GetTypeByMetadataName("System.StringComparison");
        if (comparison is null) {
            return "System.StringComparison";
        }

        var visible = context.SemanticModel
            .LookupNamespacesAndTypes(node.SpanStart, name: "StringComparison")
            .Any(symbol => SymbolEqualityComparer.Default.Equals(symbol, comparison));

        return visible ? "StringComparison" : "System.StringComparison";
    }
}
