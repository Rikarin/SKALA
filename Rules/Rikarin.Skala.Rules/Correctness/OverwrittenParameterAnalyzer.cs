using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2142</c> — a parameter every path assigns before anything reads it.
/// </summary>
/// <remarks>
///     <para>
///         The caller computed a value, passed it, and it was discarded one line into the callee.
///         Either the body meant to use it and an assignment landed in the wrong place, or the author
///         wanted a local and reused the parameter's name — opposite defects with one shape, and
///         nothing at the call site can show either.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The verdict is Roslyn's data flow rather than a syntactic scan, and that is what makes
///             the conditional cases right without a guard for each.
///         </b>
///         <c>if (f) { x = 1; } Use(x);</c> reads the incoming value on the other path, and
///         <c>x += 1</c>, <c>x++</c> and <c>x ??= y</c> all read before they write; every one of them
///         puts the parameter in <c>DataFlowsIn</c> and is silent. The finding is exactly "written
///         inside, and its incoming value flows in nowhere".
///     </para>
///     <para>
///         ⚠ <b><c>ref</c>, <c>out</c> and <c>in</c> are excluded and the exclusion is load-bearing.</b>
///         An <c>out</c> parameter has no incoming value by contract, so data flow reports every correct
///         one as discarded. Measured rather than assumed: both shapes were run through
///         <c>AnalyzeDataFlow</c> and both came back looking precisely like the defect.
///     </para>
///     <para>
///         ⚠ <b>A captured parameter is where this analysis stops.</b> Data flow over a body holding a
///         lambda or a local function that touches the parameter is not ordered the way its reader is,
///         so anything in <c>Captured</c> is left alone even where the write looks unconditional.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OverwrittenParameterAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.OverwrittenParameter);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.LocalFunctionStatement,
            SyntaxKind.OperatorDeclaration,
            SyntaxKind.ConversionOperatorDeclaration
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var (parameters, body) = Shape(context.Node);
        if (parameters is null || body is null || parameters.Parameters.Count == 0) {
            return;
        }

        var flow = context.SemanticModel.AnalyzeDataFlow(body);
        if (flow is not { Succeeded: true }) {
            return;
        }

        var written = flow.WrittenInside;
        if (written.Length == 0) {
            return;
        }

        foreach (var syntax in parameters.Parameters) {
            if (context.SemanticModel.GetDeclaredSymbol(syntax, context.CancellationToken)
                is not { RefKind: RefKind.None } parameter) {
                continue;
            }

            if (!Contains(written, parameter)
                || Contains(flow.DataFlowsIn, parameter)
                || Contains(flow.Captured, parameter)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    FirstWrite(context, body, parameter) ?? syntax.Identifier.GetLocation(),
                    "The value passed for '" + parameter.Name + "' is overwritten before anything reads it"
                )
            );
        }
    }

    /// <summary>The parameter list and the body to analyse, for each declaration shape that has both.</summary>
    static (BaseParameterListSyntax? Parameters, SyntaxNode? Body) Shape(SyntaxNode node) =>
        node switch {
            MethodDeclarationSyntax m => (m.ParameterList, Body(m.Body, m.ExpressionBody)),
            ConstructorDeclarationSyntax c => (c.ParameterList, Body(c.Body, c.ExpressionBody)),
            LocalFunctionStatementSyntax l => (l.ParameterList, Body(l.Body, l.ExpressionBody)),
            OperatorDeclarationSyntax o => (o.ParameterList, Body(o.Body, o.ExpressionBody)),
            ConversionOperatorDeclarationSyntax v => (v.ParameterList, Body(v.Body, v.ExpressionBody)),
            _ => (null, null)
        };

    static SyntaxNode? Body(BlockSyntax? block, ArrowExpressionClauseSyntax? arrow) =>
        (SyntaxNode?)block ?? arrow?.Expression;

    static bool Contains(ImmutableArray<ISymbol> symbols, IParameterSymbol parameter) {
        foreach (var symbol in symbols) {
            if (SymbolEqualityComparer.Default.Equals(symbol, parameter)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Where the discard happens, so the report lands on the assignment rather than on the
    ///     declaration the reader already believes.
    /// </summary>
    /// <remarks>
    ///     ⚠ Best effort, and the caller falls back to the parameter's identifier. Data flow has
    ///     already decided the finding; this only chooses where to point, so a shape not matched here
    ///     costs a worse location and never a wrong verdict.
    /// </remarks>
    static Location? FirstWrite(SyntaxNodeAnalysisContext context, SyntaxNode body, IParameterSymbol parameter) {
        foreach (var node in body.DescendantNodes()) {
            var target = node switch {
                AssignmentExpressionSyntax assignment => assignment.Left,
                PrefixUnaryExpressionSyntax prefix => prefix.Operand,
                PostfixUnaryExpressionSyntax postfix => postfix.Operand,
                _ => null
            };

            if (target is null) {
                continue;
            }

            foreach (var identifier in Identifiers(target)) {
                if (SymbolEqualityComparer.Default.Equals(
                        context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol,
                        parameter
                    )) {
                    return identifier.GetLocation();
                }
            }
        }

        return null;
    }

    /// <summary>
    ///     The identifiers a write targets — one for <c>x = …</c>, several for a tuple deconstruction.
    /// </summary>
    static System.Collections.Generic.IEnumerable<IdentifierNameSyntax> Identifiers(ExpressionSyntax target) {
        if (target is IdentifierNameSyntax simple) {
            yield return simple;
            yield break;
        }

        if (target is not TupleExpressionSyntax tuple) {
            yield break;
        }

        foreach (var element in tuple.Arguments) {
            if (element.Expression is IdentifierNameSyntax nested) {
                yield return nested;
            }
        }
    }
}
