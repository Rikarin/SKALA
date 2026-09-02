using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2240</c> — <c>x with { A = a, B = b }</c> naming every member is <c>new T(a, b)</c>.
/// </summary>
/// <remarks>
///     <para>
///         A <c>with</c> that assigns every positional parameter keeps nothing of the value it copies,
///         so the copy is ceremony — but the cost is not the ceremony. Add a member to the record and
///         the <c>with</c> silently starts carrying that member across from <c>x</c>, which is the one
///         value the author had already decided not to keep. <c>new T(…)</c> cannot do that: a new
///         parameter is a compile error at every call site, which is exactly why it is the safer form.
///     </para>
///     <para>
///         ⚠ <b>Decidable, and asked as a set comparison.</b> The names the initializer assigns against
///         the positional parameters of the record's primary constructor. Nothing here is a heuristic
///         about how the code looks.
///     </para>
///     <para>
///         ⚠ <b>Disjoint from <c>SK0230</c> by construction.</b> <c>SK0230</c> reports
///         <c>x with { }</c>; this requires an assignment for every positional parameter of a record
///         that has at least one, so the two can never see the same expression.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Disjoint from <c>SK1071</c>, and the guard that makes it so is what stops the pair
///             fixing each other forever.
///         </b> <c>SK1071</c> turns <c>new R(x.A, x.B, c)</c> into
///         <c>x with { C = c }</c> — an initializer setting <em>fewer</em> than all the members, which
///         this rule declines. The other direction is the live hazard:
///         <c>x with { X = x.X, Y = b }</c> assigns every member and would fix to <c>new T(x.X, b)</c>,
///         which is precisely <c>SK1071</c>'s input and would be fixed straight back. Any assignment
///         carrying a member across unchanged from the same receiver therefore withdraws the finding.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WithExpressionRewritesAllAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.WithExpressionRewritesAll);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.WithExpressionRewritesAll);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.WithExpression);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var with = (WithExpressionSyntax)context.Node;

        // ⚠ A simple name, and nothing else. `GetPoint() with { X = a, Y = b }` rewrites to
        // `new Point(a, b)`, which drops the call the original made — an evaluation the replacement
        // does not perform. `SK0230` reports the empty initializer and this rule never sees one.
        if (with.Expression is not IdentifierNameSyntax receiver || with.Initializer.Expressions.Count == 0) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (model.GetSymbolInfo(receiver, cancellation).Symbol is not (ILocalSymbol or IParameterSymbol)) {
            return;
        }

        if (model.GetTypeInfo(receiver, cancellation).Type is not INamedTypeSymbol record
            || RecordShape.PrimaryConstructor(record, cancellation) is not { } constructor
            || !RecordShape.WholeStateIsItsParameters(record, constructor, cancellation)) {
            return;
        }

        // ⚠ The count is an early-out and *not* the guard, which a sabotage established: weakening it
        // to `>` turns no fixture red. The loop below looks every positional parameter up by name and
        // returns as soon as one is missing, and `WholeStateIsItsParameters` has already rejected any
        // settable member outside the parameter list — so the initializer cannot assign a name that is
        // not a parameter, and the counts cannot disagree in the direction the loop misses.
        if (Assignments(model, with, receiver, cancellation) is not { } assigned
            || assigned.Count != constructor.Parameters.Length) {
            return;
        }

        // The set comparison the rule *is*: every positional parameter assigned, none left to copy.
        if (InParameterOrder(constructor, assigned) is not { } arguments) {
            return;
        }

        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(with.SyntaxTree, with.Span)
            || NullComparison.InsideExpressionTree(model, with, cancellation)) {
            return;
        }

        var replacement = Construction(record.ToMinimalDisplayString(model, with.SpanStart), arguments);

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                with.GetLocation(),
                FixEdits.Pack((with.Span, replacement)),
                "every member of `"
                + record.Name
                + "` is assigned, so nothing of `"
                + receiver.Identifier.ValueText
                + "` survives the copy: `"
                + RewriteGuards.Trim(replacement)
                + "`"
            )
        );
    }

    /// <summary>
    ///     The assigned values in the constructor's parameter order, or null where one is missing.
    /// </summary>
    /// <remarks>
    ///     ⚠ This lookup, and not the count compared against it above, is what requires every positional
    ///     parameter to have been assigned — the initializer is free to list them in any order, and
    ///     <c>pair with { Second = b, First = a }</c> has to emit <c>new Pair(a, b)</c>.
    /// </remarks>
    static ExpressionSyntax[]? InParameterOrder(
        IMethodSymbol constructor,
        Dictionary<string, ExpressionSyntax> assigned
    ) {
        var arguments = new ExpressionSyntax[constructor.Parameters.Length];
        for (var i = 0; i < constructor.Parameters.Length; i++) {
            if (!assigned.TryGetValue(constructor.Parameters[i].Name, out var value)) {
                return null;
            }

            arguments[i] = value;
        }

        return arguments;
    }

    /// <summary>The replacement text: a constructor call on the record, argument for argument.</summary>
    static string Construction(string type, ExpressionSyntax[] arguments) {
        var text = new StringBuilder("new ").Append(type).Append('(');
        for (var i = 0; i < arguments.Length; i++) {
            if (i > 0) {
                text.Append(", ");
            }

            text.Append(arguments[i].ToString());
        }

        return text.Append(')').ToString();
    }

    /// <summary>
    ///     The initializer read as a member-name to value map, or null where any part of it disqualifies.
    /// </summary>
    /// <remarks>
    ///     ⚠ The fix-loop guard lives here. An assignment whose value is the same member read off the
    ///     same receiver — <c>x with { X = x.X, … }</c> — would fix to <c>new T(x.X, …)</c>, which is
    ///     <c>SK1071</c>'s shape exactly, and the two rules would rewrite each other forever. It is also
    ///     not what this rule is about: a member copied across by hand is a copy, not a rewrite.
    /// </remarks>
    static Dictionary<string, ExpressionSyntax>? Assignments(
        SemanticModel model,
        WithExpressionSyntax with,
        IdentifierNameSyntax receiver,
        CancellationToken cancellation
    ) {
        var result = new Dictionary<string, ExpressionSyntax>(System.StringComparer.Ordinal);
        foreach (var expression in with.Initializer.Expressions) {
            cancellation.ThrowIfCancellationRequested();
            if (expression is not AssignmentExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                    Left: IdentifierNameSyntax member
                } assignment) {
                return null;
            }

            if (CarriesAcross(assignment.Right, member, receiver)) {
                return null;
            }

            // A duplicate assignment is `SK2100`'s subject and leaves this rule's count wrong.
            if (result.ContainsKey(member.Identifier.ValueText)) {
                return null;
            }

            result[member.Identifier.ValueText] = assignment.Right;
        }

        return result;
    }

    /// <summary>
    ///     Whether the value is the same member read off the same receiver.
    /// </summary>
    static bool CarriesAcross(ExpressionSyntax value, IdentifierNameSyntax member, IdentifierNameSyntax receiver) =>
        value is MemberAccessExpressionSyntax {
            RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
            Expression: IdentifierNameSyntax source,
            Name: IdentifierNameSyntax read
        }
        && string.Equals(source.Identifier.ValueText, receiver.Identifier.ValueText, System.StringComparison.Ordinal)
        && string.Equals(read.Identifier.ValueText, member.Identifier.ValueText, System.StringComparison.Ordinal);
}
