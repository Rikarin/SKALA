using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1050</c> — the test-and-cast family that <c>SK1015</c> does not already own.
/// </summary>
/// <remarks>
///     ⚠ <b><c>SK1015</c> is `is T` followed by a cast, and nothing here repeats it.</b> Four other
///     shapes ask the same question the long way round:
///     <list type="number">
///         <item>
///             <c>var b = x as T;</c> immediately followed by <c>if (b != null)</c> — the conversion,
///             the storage and the test are one <c>x is T b</c>.
///         </item>
///         <item><c>(x as T) != null</c> used as a boolean — a conversion performed to be thrown away.</item>
///         <item><c>!(x is T)</c> — the negation of a question, rather than the question.</item>
///         <item>
///             <c>s is object</c> where the operand's own static type already converts to the tested
///             type — a type check that succeeds for everything non-null, which is to say a null check.
///         </item>
///     </list>
///     <para>
///         ⚠ <b>The scoping half is what makes shape 1 hard, not the rewrite.</b> A pattern variable is
///         not definitely assigned after the <c>if</c> that introduces it, while a local declared above
///         one is. So every reference to the local has to be inside the guarded <c>if</c> before the
///         declaration may move into its condition, and an <c>else</c> branch — where the original
///         local is legibly null and the pattern variable is unassigned — withdraws the rule entirely.
///     </para>
///     <para>
///         ⚠ <b>Shapes 3 and 4 produce a <c>not</c> pattern and are gated at C# 9 individually</b>,
///         while the rule's declared floor is C# 7 for shape 1. One floor for the rule would either
///         silence the C# 7 shape on a C# 7 project or emit C# 9 syntax into one.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestAndCastPatternAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.PatternMatchingOverTestAndCast);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.PatternMatchingOverTestAndCast);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                var patterns = SkalaRule.MeetsLanguageVersion(start.Compilation, "9.0");
                start.RegisterSyntaxNodeAction(AnalyzeConversionThenNullCheck, SyntaxKind.LocalDeclarationStatement);
                start.RegisterSyntaxNodeAction(
                    context => AnalyzeSafeCastAsTypeCheck(context, patterns),
                    SyntaxKind.NotEqualsExpression,
                    SyntaxKind.EqualsExpression
                );

                if (patterns) {
                    start.RegisterSyntaxNodeAction(AnalyzeNegatedTypeCheck, SyntaxKind.LogicalNotExpression);
                    start.RegisterSyntaxNodeAction(AnalyzeAlwaysSucceedingTypeCheck, SyntaxKind.IsExpression);
                }
            }
        );
    }

    /// <summary>Shape 1: <c>var b = x as T;</c> then <c>if (b != null)</c>.</summary>
    static void AnalyzeConversionThenNullCheck(SyntaxNodeAnalysisContext context) {
        var statement = (LocalDeclarationStatementSyntax)context.Node;
        if (statement.UsingKeyword.RawKind != (int)SyntaxKind.None
            || statement.AwaitKeyword.RawKind != (int)SyntaxKind.None
            || statement.Modifiers.Count > 0
            || statement.Declaration.Variables.Count != 1
            || statement.Parent is not BlockSyntax block) {
            return;
        }

        var declarator = statement.Declaration.Variables[0];
        if (declarator.Initializer?.Value is not BinaryExpressionSyntax {
                RawKind: (int)SyntaxKind.AsExpression
            } conversion
            || conversion.Right is not TypeSyntax tested
            || !RewriteGuards.IsPlainNamePath(conversion.Left)) {
            return;
        }

        var index = block.Statements.IndexOf(statement);
        if (index < 0
            || index + 1 >= block.Statements.Count
            || block.Statements[index + 1] is not IfStatementSyntax { Else: null } guard) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        var name = declarator.Identifier.ValueText;
        if (LeadingNullCheck(guard.Condition, name) is not { } check
            || !IsReferenceTypeTest(model, tested, cancellation)) {
            return;
        }

        // ⚠ `!=` calls whatever `operator !=` the local's type declares; `is T b` is a pattern and
        // always tests the conversion. On a type with its own operator those are different programs.
        if (check is BinaryExpressionSyntax comparison
            && NullComparison.OperandOf(comparison) is { } operand
            && !NullComparison.IsRewritable(model, operand, cancellation)) {
            return;
        }

        if (model.GetDeclaredSymbol(declarator, cancellation) is not ILocalSymbol {
                RefKind: RefKind.None,
                IsConst: false
            } local) {
            return;
        }

        // ⚠ A declaration pattern binds the variable to the *tested* type, so an explicitly typed
        // declaration is admitted only where the two agree. `object o = x as Widget;` does not.
        var testedType = model.GetTypeInfo(tested, cancellation).Type;
        if (!statement.Declaration.Type.IsVar
            && (testedType is null
                || !SymbolEqualityComparer.Default.Equals(
                    model.GetTypeInfo(statement.Declaration.Type, cancellation).Type,
                    testedType
                ))) {
            return;
        }

        // ⚠ The definite-assignment half. `b` is assigned above the `if` today and merely *declared*
        // by the pattern, so a read below the `if` becomes CS0165 and a read in an `else` branch —
        // already refused above — would be a read of a variable the pattern never bound.
        if (RewriteGuards.ReferencedOutside(model, local, guard, statement, cancellation)) {
            return;
        }

        if (NullComparison.InsideExpressionTree(model, statement, cancellation)
            || RewriteGuards.ContainsCommentOrDirective(statement)
            || RewriteGuards.ContainsCommentOrDirective(statement.SyntaxTree, statement.FullSpan)
            || RewriteGuards.ContainsCommentOrDirective(check)) {
            return;
        }

        var replacement = conversion.Left + " is " + tested + " " + name;
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(statement.SyntaxTree, check.Span),
                FixEdits.Pack(
                    (check.Span, replacement),
                    (RewriteGuards.LineSpanOf(statement), string.Empty)
                ),
                "The conversion and the null check are one pattern: `" + RewriteGuards.Trim(replacement) + "`"
            )
        );
    }

    /// <summary>Shape 2: <c>(x as T) != null</c> is <c>x is T</c>.</summary>
    static void AnalyzeSafeCastAsTypeCheck(SyntaxNodeAnalysisContext context, bool patterns) {
        var comparison = (BinaryExpressionSyntax)context.Node;
        var negated = comparison.IsKind(SyntaxKind.EqualsExpression);
        if (negated && !patterns) {
            return;
        }

        if (NullComparison.OperandOf(comparison) is not { } operand
            || PatternSafety.Unwrap(operand) is not BinaryExpressionSyntax {
                RawKind: (int)SyntaxKind.AsExpression
            } conversion
            || conversion.Right is not TypeSyntax tested) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (!IsReferenceTypeTest(model, tested, cancellation)
            || !NullComparison.IsRewritable(model, operand, cancellation)
            || NullComparison.InsideExpressionTree(model, comparison, cancellation)
            || RewriteGuards.ContainsCommentOrDirective(comparison)
            || !IsPatternSafeContext(comparison)) {
            return;
        }

        var replacement = conversion.Left + (negated ? " is not " : " is ") + tested;
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                comparison.GetLocation(),
                FixEdits.Pack((comparison.Span, replacement)),
                "The safe cast is used as a type check: `" + RewriteGuards.Trim(replacement) + "`"
            )
        );
    }

    /// <summary>Shape 3: <c>!(x is T)</c> is <c>x is not T</c>.</summary>
    static void AnalyzeNegatedTypeCheck(SyntaxNodeAnalysisContext context) {
        var negation = (PrefixUnaryExpressionSyntax)context.Node;
        if (PatternSafety.Unwrap(negation.Operand) is not BinaryExpressionSyntax {
                RawKind: (int)SyntaxKind.IsExpression
            } test
            || test.Right is not TypeSyntax tested) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetTypeInfo(tested, cancellation).Type is not { } type
            || type.TypeKind == TypeKind.Error
            || NullComparison.InsideExpressionTree(model, negation, cancellation)
            || RewriteGuards.ContainsCommentOrDirective(negation)
            || !IsPatternSafeContext(negation)) {
            return;
        }

        var replacement = test.Left + " is not " + tested;
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                negation.GetLocation(),
                FixEdits.Pack((negation.Span, replacement)),
                "The negated type check is a negated pattern: `" + RewriteGuards.Trim(replacement) + "`"
            )
        );
    }

    /// <summary>Shape 4: a type check that succeeds for every non-null value is a null check.</summary>
    static void AnalyzeAlwaysSucceedingTypeCheck(SyntaxNodeAnalysisContext context) {
        var test = (BinaryExpressionSyntax)context.Node;
        if (test.Right is not TypeSyntax tested) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        var source = model.GetTypeInfo(test.Left, cancellation).Type;
        var target = model.GetTypeInfo(tested, cancellation).Type;

        // ⚠ Reference types only, in both positions. A value type is never null, so the rewrite would
        // be `int is not null` — which is not the same claim and does not compile the same way — and
        // an unconstrained type parameter's `is object` depends on the type argument.
        if (source is null
            || target is null
            || source.TypeKind is TypeKind.Error or TypeKind.Dynamic or TypeKind.TypeParameter
            || target.TypeKind is TypeKind.Error or TypeKind.Dynamic or TypeKind.TypeParameter
            || !source.IsReferenceType
            || !target.IsReferenceType) {
            return;
        }

        // The test says nothing beyond "not null" only when the operand's own static type already
        // converts to the tested one: `string is object`, `Binary is Node`, `List<int> is IEnumerable`.
        var conversion = model.Compilation.ClassifyConversion(source, target);
        if (!conversion.IsIdentity && !(conversion.IsImplicit && conversion.IsReference)) {
            return;
        }

        if (NullComparison.InsideExpressionTree(model, test, cancellation)
            || RewriteGuards.ContainsCommentOrDirective(test)
            || !IsPatternSafeContext(test)) {
            return;
        }

        var replacement = test.Left + " is not null";
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                test.GetLocation(),
                FixEdits.Pack((test.Span, replacement)),
                "The type check succeeds for everything non-null: `" + RewriteGuards.Trim(replacement) + "`"
            )
        );
    }

    /// <summary>
    ///     The null check a condition opens with, or null when it does not open with one on
    ///     <paramref name="name" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ The leftmost operand of a <c>&amp;&amp;</c> chain counts, because <c>&amp;&amp;</c>
    ///     short-circuits: <c>if (b != null &amp;&amp; b.Left is null)</c> reads <c>b</c> only after the
    ///     check, so <c>x is T b &amp;&amp; b.Left is null</c> is the same program. Anything else —
    ///     <c>||</c>, a negation, the check appearing second — is not, and is refused.
    /// </remarks>
    static ExpressionSyntax? LeadingNullCheck(ExpressionSyntax condition, string name) {
        var current = PatternSafety.Unwrap(condition);
        while (current is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalAndExpression } conjunction) {
            current = PatternSafety.Unwrap(conjunction.Left);
        }

        if (current is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.NotEqualsExpression } comparison
            && NullComparison.OperandOf(comparison) is IdentifierNameSyntax identifier
            && string.Equals(identifier.Identifier.ValueText, name, System.StringComparison.Ordinal)) {
            return comparison;
        }

        if (current is IsPatternExpressionSyntax {
                Expression: IdentifierNameSyntax subject,
                Pattern:
                UnaryPatternSyntax {
                    RawKind: (int)SyntaxKind.NotPattern,
                    Pattern: ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.NullLiteralExpression }
                }
            }
            && string.Equals(subject.Identifier.ValueText, name, System.StringComparison.Ordinal)) {
            return current;
        }

        return null;
    }

    /// <summary>Whether the tested type is a resolved reference type, so <c>is T</c> is a legal pattern.</summary>
    static bool IsReferenceTypeTest(SemanticModel model, TypeSyntax tested, CancellationToken cancellation) {
        var type = model.GetTypeInfo(tested, cancellation).Type;
        return type is { TypeKind: not (TypeKind.Error or TypeKind.Dynamic) } && type.IsReferenceType;
    }

    /// <summary>
    ///     ⚠ Whether an <c>is</c> expression may be dropped into this position without parentheses.
    /// </summary>
    /// <remarks>
    ///     A pattern's grammar is not an expression's. <c>!(x is T)</c> rewritten bare as
    ///     <c>!x is not T</c> is <c>(!x) is not T</c>, and <c>a is object == b</c> rewritten as
    ///     <c>a is not null == b</c> hands <c>null == b</c> to a grammar that parses constant patterns.
    ///     Rather than inventing parentheses the author did not write — which the formatter is not
    ///     allowed to remove again — the rule declines every position outside this list.
    /// </remarks>
    static bool IsPatternSafeContext(ExpressionSyntax expression) {
        var parent = expression.Parent;
        return parent switch {
            ParenthesizedExpressionSyntax => true,
            IfStatementSyntax or WhileStatementSyntax or DoStatementSyntax => true,
            ReturnStatementSyntax or ExpressionStatementSyntax or ArrowExpressionClauseSyntax => true,
            ArgumentSyntax or AttributeArgumentSyntax or EqualsValueClauseSyntax => true,
            AssignmentExpressionSyntax assignment => assignment.Right == expression,
            ConditionalExpressionSyntax conditional => conditional.Condition == expression,
            BinaryExpressionSyntax binary => binary.IsKind(SyntaxKind.LogicalAndExpression)
                || binary.IsKind(SyntaxKind.LogicalOrExpression),
            _ => false
        };
    }
}
