using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2134</c> — an instance member assigning a static field of its own type.
/// </summary>
/// <remarks>
///     The write reads as per-instance initialization and is not: there is one slot, so the last
///     instance to run the line wins for every other instance, on every thread, for the rest of the
///     process.
///     <para>
///         ⚠ <b><c>CA2211</c> is a different question and was verified to be.</b> A probe at
///         <c>AnalysisMode=All</c> reports <c>CA2211</c> on a <c>public static</c> field and says
///         nothing about a <c>private static</c> one — it is about a field's <em>visibility</em>, it
///         lands on the declaration, and it is silent about who writes it. This rule never looks at
///         visibility and never reports a declaration; it reports the write. The two overlap on exactly
///         one shape, a public static field written from instance code, and there they are saying two
///         different true things about it.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Lazy initialization is the look-alike, and it is declined by recognising the guard
///             rather than by recognising the name.
///         </b> <c>_instance ??= new()</c> is instance code writing
///         static state on purpose: the guard is what makes it write-once, so the "last one wins"
///         complaint is not true of it. A <c>??=</c>, and an assignment under an <c>if</c> testing that
///         same field against <c>null</c> or <c>default</c>, are both declined — the second form covers
///         double-checked locking, where the inner test is the one the walk finds.
///     </para>
///     <para>
///         ⚠ <b>Only the enclosing type's own static fields.</b> <c>Console.Out = …</c> and
///         <c>CultureInfo.DefaultThreadCurrentCulture = …</c> are process-wide settings being set on
///         purpose, usually exactly once, by code that knows it; they are a different concept with a
///         different answer, and including them would have made the decline list carry the rule.
///     </para>
///     <para>
///         ⚠ <b>A write inside a lambda or a local function is declined</b>, because where it runs is
///         not where it is written: a delegate handed to a scheduler is no longer instance code in the
///         sense the finding means, and the rule would be asserting something it cannot see.
///         <c>Interlocked.Increment(ref total)</c> is declined by construction rather than by a filter
///         — it is an argument, not an assignment, and no assignment node exists for the rule to visit.
///     </para>
///     <para>
///         ⚠
///         <b>
///             A counter incremented from a constructor is reported, and that is the intended
///             behaviour rather than an oversight.
///         </b> <c>static int count; C() { count++; }</c> is the
///         canonical shape of this concept: it is shared mutable state, it is not atomic, and two
///         threads constructing at once lose an increment. The fixtures pin it in the positive
///         direction so that nobody later mistakes it for a false positive and adds an exclusion.
///     </para>
///     <para>
///         Report-only. The two repairs — make the state per-instance, or make the member static —
///         change the type's shape in opposite directions, and which one is right is a design decision
///         that the assignment alone does not contain.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InstanceWriteToStaticAnalyzer : DiagnosticAnalyzer {
    const string ThreadStaticAttribute = "System.ThreadStaticAttribute";

    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.InstanceWriteToStatic);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxKind.CoalesceAssignmentExpression,
            SyntaxKind.AddAssignmentExpression,
            SyntaxKind.SubtractAssignmentExpression,
            SyntaxKind.MultiplyAssignmentExpression,
            SyntaxKind.DivideAssignmentExpression,
            SyntaxKind.ModuloAssignmentExpression,
            SyntaxKind.AndAssignmentExpression,
            SyntaxKind.OrAssignmentExpression,
            SyntaxKind.ExclusiveOrAssignmentExpression,
            SyntaxKind.LeftShiftAssignmentExpression,
            SyntaxKind.RightShiftAssignmentExpression,
            SyntaxKind.UnsignedRightShiftAssignmentExpression,
            SyntaxKind.PreIncrementExpression,
            SyntaxKind.PreDecrementExpression,
            SyntaxKind.PostIncrementExpression,
            SyntaxKind.PostDecrementExpression
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var (target, verb) = context.Node switch {
            AssignmentExpressionSyntax assignment => (assignment.Left, "writes"),
            PrefixUnaryExpressionSyntax prefix => (prefix.Operand, "updates"),
            PostfixUnaryExpressionSyntax postfix => (postfix.Operand, "updates"),
            _ => (null, string.Empty)
        };

        if (target is null) {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(target, context.CancellationToken).Symbol is not IFieldSymbol field
            || !field.IsStatic
            || field.IsConst) {
            return;
        }

        var member = EnclosingInstanceMember(context, context.Node);
        if (member is null || !SymbolEqualityComparer.Default.Equals(field.ContainingType, member.ContainingType)) {
            return;
        }

        if (IsThreadStatic(field) || Guarded(context, context.Node, field)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                context.Node.GetLocation(),
                "the instance "
                + Describe(member)
                + " `"
                + member.Name
                + "` "
                + verb
                + " the static field `"
                + field.Name
                + "`, which every instance and every thread shares"
            )
        );
    }

    static string Describe(ISymbol member) =>
        member is IMethodSymbol { MethodKind: MethodKind.Constructor } ? "constructor" : "member";

    /// <summary>
    ///     The instance member whose body contains this node, or null when there is none — because the
    ///     member is <c>static</c>, or because a lambda or a local function sits in between.
    /// </summary>
    static ISymbol? EnclosingInstanceMember(SyntaxNodeAnalysisContext context, SyntaxNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            // ⚠ Deferred code first, and before the member test rather than after it. A lambda inside
            // an instance method still has an instance member above it, so checking the member first
            // would report a write whose "when" the rule cannot see.
            if (current is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax) {
                return null;
            }

            // An accessor's staticness is its property's, and the property declaration is above it.
            if (current is not MemberDeclarationSyntax) {
                continue;
            }

            var symbol = current is FieldDeclarationSyntax fieldDeclaration
                ? context.SemanticModel.GetDeclaredSymbol(
                    fieldDeclaration.Declaration.Variables[0],
                    context.CancellationToken
                )
                : context.SemanticModel.GetDeclaredSymbol(current, context.CancellationToken);

            // ⚠ A namespace declaration is a `MemberDeclarationSyntax` too, and its symbol is not
            // static — so accepting "anything that is not a type" here would treat the enclosing
            // namespace as the instance member and report every static write in the file.
            if (symbol is not IMethodSymbol and not IPropertySymbol and not IFieldSymbol and not IEventSymbol) {
                return null;
            }

            return symbol.IsStatic ? null : symbol;
        }

        return null;
    }

    static bool IsThreadStatic(IFieldSymbol field) {
        foreach (var attribute in field.GetAttributes()) {
            if (attribute.AttributeClass is { } type
                && string.Equals(
                    type.ContainingNamespace?.ToDisplayString() + "." + type.Name,
                    ThreadStaticAttribute,
                    StringComparison.Ordinal
                )) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether the write is a lazy initialization: a <c>??=</c>, or an assignment under a condition
    ///     that tests the very field being written against <c>null</c> or <c>default</c>.
    /// </summary>
    /// <remarks>
    ///     The condition test is deliberately loose — the field mentioned anywhere in it together with
    ///     the word <c>null</c> or <c>default</c> anywhere in it is enough. Every shape it recognises is
    ///     a reason to stay silent, so a guard test that is too generous costs findings and never
    ///     produces one.
    /// </remarks>
    static bool Guarded(SyntaxNodeAnalysisContext context, SyntaxNode node, IFieldSymbol field) {
        if (node.IsKind(SyntaxKind.CoalesceAssignmentExpression)) {
            return true;
        }

        if (node is AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } simple
            && simple.Right.IsKind(SyntaxKind.CoalesceExpression)) {
            return true;
        }

        for (var current = node.Parent; current is not null; current = current.Parent) {
            if (current is MemberDeclarationSyntax) {
                return false;
            }

            var condition = current switch {
                IfStatementSyntax statement => statement.Condition,
                ConditionalExpressionSyntax conditional => conditional.Condition,
                WhileStatementSyntax loop => loop.Condition,
                _ => null
            };

            if (condition is not null && TestsForAbsence(context, condition, field)) {
                return true;
            }
        }

        return false;
    }

    static bool TestsForAbsence(SyntaxNodeAnalysisContext context, ExpressionSyntax condition, IFieldSymbol field) {
        var absence = false;
        var mentioned = false;

        foreach (var node in condition.DescendantNodesAndSelf()) {
            if (node.IsKind(SyntaxKind.NullLiteralExpression)
                || node.IsKind(SyntaxKind.DefaultLiteralExpression)
                || node.IsKind(SyntaxKind.DefaultExpression)
                || node.IsKind(SyntaxKind.ConstantPattern)) {
                absence = true;
            }

            if (node is ExpressionSyntax expression
                && SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol,
                    field
                )) {
                mentioned = true;
            }
        }

        return absence && mentioned;
    }
}
