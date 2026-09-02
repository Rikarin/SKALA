using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2213</c> — a search that reports absence as <c>-1</c>, tested with <c>&gt; 0</c>.
/// </summary>
/// <remarks>
///     <c>IndexOf</c> answers two questions at once — whether the value is there, and where — and
///     <c>&gt; 0</c> conflates them, so the element at position 0 is present and reported absent. The
///     test survives every fixture that does not begin with the needle, which is most of them.
///     <para>
///         ⚠ <b>This is the carve-out <c>SK2053</c> names and declines.</b> <c>SK2053</c> proves a
///         comparison from the framework contract that a count is never negative;
///         <c>IndexOf</c> is the framework member whose negative result is <em>meaningful</em>, which is
///         why <c>SK2053</c>'s own false-positive note excludes it and why the two can never report the
///         same expression. <c>SK2001</c> is further away still: it decides a comparison from the
///         operand <em>type's</em> range, and <c>IndexOf</c> returns an <c>int</c>, whose range settles
///         nothing about zero.
///     </para>
///     <para>
///         ⚠ <b><c>&gt; 0</c> is correct when "found, but not at the start" is meant</b>, and the rule
///         cannot tell the two readings apart — which is why <c>fixIsSafe</c> is <c>false</c>. The
///         escape hatch is the unambiguous spelling: <c>&gt;= 1</c> says the same thing with no second
///         reading and is deliberately not reported.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IndexOfComparedToPositiveAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.IndexOfComparedToPositive);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.GreaterThanExpression, SyntaxKind.LessThanExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var binary = (BinaryExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        // A user-defined `>` is somebody else's semantics, and a lifted one can be neither true nor
        // false in the way the rewrite assumes.
        //
        // ⚠ Both clauses are unreachable, and a sabotage is what proved it rather than reasoning
        // afterwards: removing them turned no fixture red. The search must return `System.Int32` and
        // the constant must be the literal `0`, so the comparison is always the *built-in* `int >
        // int` — a user-defined operator cannot be selected for two `int` operands, and a lifted one
        // would need an `int?` that no covered `IndexOf` returns. Kept as the statement of intent,
        // the way SK2053 keeps its own `IsLifted` clause, and not credited as the thing that works.
        if (model.GetOperation(binary, cancellation) is not IBinaryOperation { OperatorMethod: null, IsLifted: false }) {
            return;
        }

        // `search > 0` and the mirrored `0 < search`. ⚠ The mirrored form's fix is `<=`, not `>=`:
        // the operator token is rewritten where it stands, so which side the search is on decides
        // which token it becomes.
        var mirrored = binary.Kind() == SyntaxKind.LessThanExpression;
        var search = mirrored ? binary.Right : binary.Left;
        var zero = mirrored ? binary.Left : binary.Right;

        // `>= 1` is the unambiguous spelling of "found, but not at the start" and is not a registered
        // kind, so it can never reach here.
        if (model.GetConstantValue(zero, cancellation) is not { HasValue: true, Value: 0 }
            || model.GetOperation(search, cancellation) is not IInvocationOperation invocation
            || !IsSearchReturningMinusOne(invocation.TargetMethod)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                binary.OperatorToken.GetLocation(),
                FixEdits.Pack((binary.OperatorToken.Span, mirrored ? "<=" : ">=")),
                "'"
                + invocation.TargetMethod.Name
                + "' returns 0 for a match at the first position, which this test rejects along with -1"
            )
        );
    }

    /// <summary>
    ///     ⚠ A closed set, not a name match. A method called <c>IndexOf</c> on a type outside these
    ///     contracts may return anything at all — the same reasoning that stops <c>SK2053</c> trusting a
    ///     hand-written <c>Count</c>.
    /// </summary>
    static bool IsSearchReturningMinusOne(IMethodSymbol method) {
        if (method.Name is not ("IndexOf" or "LastIndexOf" or "IndexOfAny" or "LastIndexOfAny")
            || method.ReturnType.SpecialType != SpecialType.System_Int32) {
            return false;
        }

        var owner = method.ContainingType;
        return owner.SpecialType is SpecialType.System_String or SpecialType.System_Array
            || owner.ToDisplayString() == "System.MemoryExtensions"
            || IsListContract(owner)
            || owner.AllInterfaces.Any(IsListContract);
    }

    static bool IsListContract(INamedTypeSymbol type) =>
        type.OriginalDefinition.ToDisplayString() is "System.Collections.IList"
            or "System.Collections.Generic.IList<T>"
            or "System.Collections.Generic.List<T>";
}
