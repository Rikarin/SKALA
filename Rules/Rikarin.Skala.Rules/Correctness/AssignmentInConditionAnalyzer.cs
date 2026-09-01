using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2060</c> — the whole condition is an assignment, so it stores and then tests what it
///     stored.
/// </summary>
/// <remarks>
///     <c>if (x = y)</c> compiles only where the assigned type is <c>bool</c>, which is why the
///     compiler is silent and why this rule can exist at all.
///     <para>
///         ⚠ <b>"The whole condition" is the entire discriminator.</b> The read-assign-test idiom —
///         <c>while ((line = reader.ReadLine()) != null)</c> — puts an assignment <em>inside</em> a
///         condition and is correct and common. A rule that reported assignments anywhere in a
///         condition would fire on every stream loop ever written. The assignment must <b>be</b> the
///         condition.
///     </para>
///     <para>
///         ⚠ <b>An extra pair of parentheses withdraws the finding.</b> <c>if ((ok = TryLoad()))</c> is
///         the convention C programmers have used since before C# existed to say the assignment was
///         meant; gcc's <c>-Wparentheses</c> honours it and so does this. Without the exemption there
///         would be no way at all to write the intended program, which is how a correctness rule turns
///         into a rule people disable.
///     </para>
///     <para>
///         ⚠ Only <b>simple</b> assignment. <c>flag |= Check()</c> in a condition is a different shape
///         with a different repair — it is not a mistyped <c>==</c> — and folding it in here would make
///         one message describe two defects.
///     </para>
///     <para>
///         ⚠ <b>A conditional expression is deliberately not examined, and the reason is the grammar.</b>
///         The first draft registered for <c>ConditionalExpression</c> as well and it was dead code:
///         a <c>?:</c> condition is a <c>null_coalescing_expression</c>, and assignment binds looser
///         than <c>?:</c>, so <c>x = flag = other ? 1 : 2</c> parses as
///         <c>x = (flag = (other ? 1 : 2))</c> and a bare assignment can never <em>be</em> a ternary
///         condition. Writing one requires parentheses, which is the deliberate spelling this rule
///         already exempts. <c>AssignmentInConditionAnalyzerTests.ATernaryConditionCanNeverBeABareAssignment</c>
///         pins the claim, because a guard that cannot fire looks exactly like a guard that works.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AssignmentInConditionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.AssignmentInCondition);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.IfStatement,
            SyntaxKind.WhileStatement,
            SyntaxKind.DoStatement,
            SyntaxKind.ForStatement
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var condition = context.Node switch {
            IfStatementSyntax statement => statement.Condition,
            WhileStatementSyntax statement => statement.Condition,
            DoStatementSyntax statement => statement.Condition,

            // ⚠ `for`'s initialiser and incrementor are assignments by design and are not conditions.
            // Only the middle clause is read, and it is optional.
            ForStatementSyntax statement => statement.Condition,
            _ => null
        };

        // The parentheses the `if` itself carries are not part of the condition node, so a
        // ParenthesizedExpressionSyntax here is a pair the author wrote — the deliberate spelling.
        if (condition is not AssignmentExpressionSyntax assignment
            || !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                assignment.OperatorToken.GetLocation(),
                "The condition is an assignment, not a comparison; write `==`, or wrap it in its own "
                + "parentheses if assigning here was meant"
            )
        );
    }
}
