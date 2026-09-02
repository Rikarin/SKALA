using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1122</c> — two anonymous types in one member that differ only in the order of their
///     members, and are therefore two types.
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         The premise was established by compiling and running it, and half of what the inspection
///         name suggests turned out to be a no-op the compiler already performs.
///     </b> Two anonymous object
///     creations with the same member names, the same member types <em>and the same order</em> are
///     already <b>one</b> type — the same <c>Type</c> instance, across methods, within an assembly —
///     so "reuse the nearby anonymous type" has nothing to do there and this rule is silent.
///     <para>
///         ⚠ <b>Order is what creates a second type.</b> <c>new { X = 1, Y = "s" }</c> compiles to
///         <c>&lt;&gt;f__AnonymousType0`2</c> and <c>new { Y = "s", X = 1 }</c> to
///         <c>&lt;&gt;f__AnonymousType1`2</c>, and they are distinct at run time. That is the whole
///         finding: every dictionary, <c>Distinct</c>, <c>Union</c> and cache over the pair keys
///         separately, and no assignment between them compiles. A member <em>name</em> or a member
///         <em>type</em> that differs is a genuinely different shape and is not reported.
///     </para>
///     <para>
///         ⚠ <b>Reordering is only free where the initializers are.</b> The edit moves the
///         initializer expressions past one another, so it changes the order in which they are
///         evaluated; the rule matches only a chain of plain names, for which that is unobservable.
///         ⚠ <b><c>fixIsSafe: false</c> even so</b>, because the runtime type is what the fix
///         changes — that is the point — and the member order is visible through <c>ToString()</c>
///         and through anything that serialises the object.
///     </para>
///     <para>
///         ⚠ The two must be in the same member. Anonymous types unify across a whole assembly, so a
///         wider search would report a pair whose two halves have nothing to do with each other and
///         cannot be read side by side, which is the "nearby" in the inspection's own name.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReorderedAnonymousTypeAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.ReorderedAnonymousType);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ReorderedAnonymousType);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AnonymousObjectCreationExpression);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var creation = (AnonymousObjectCreationExpressionSyntax)context.Node;
        if (creation.Initializers.Count < 2) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (!TryReadShape(model, creation, cancellation, out var shape)) {
            return;
        }

        // The earliest creation in the same member whose members are this one's, reordered. Taking
        // the earliest makes the pair — and therefore the fix — the same whichever one is analysed.
        AnonymousObjectCreationExpressionSyntax? model0 = null;
        List<string>? order = null;
        foreach (var other in RewriteGuards.ScopeRoot(creation)
                     .DescendantNodes()
                     .OfType<AnonymousObjectCreationExpressionSyntax>()) {
            cancellation.ThrowIfCancellationRequested();
            if (other.SpanStart >= creation.SpanStart
                || !TryReadShape(model, other, cancellation, out var candidate)
                || !IsReorderOf(shape, candidate)) {
                continue;
            }

            model0 = other;
            order = candidate.Select(static member => member.Name).ToList();
            break;
        }

        if (model0 is null || order is null) {
            return;
        }

        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(creation.SyntaxTree, creation.Span)) {
            return;
        }

        var byName = shape.ToDictionary(static member => member.Name, static member => member.Syntax);
        var replacement = "new { "
            + string.Join(", ", order.Select(name => byName[name].ToString()))
            + " }";

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                creation.GetLocation(),
                FixEdits.Pack((creation.Span, replacement)),
                "The anonymous type on line "
                + (model0.GetLocation().GetLineSpan().StartLinePosition.Line + 1)
                + " has these members in another order, so the two are different types"
            )
        );
    }

    /// <summary>
    ///     Every member of one anonymous object creation, as (name, type, syntax), or nothing when
    ///     the creation is not one this rule may reorder.
    /// </summary>
    static bool TryReadShape(
        SemanticModel model,
        AnonymousObjectCreationExpressionSyntax creation,
        System.Threading.CancellationToken cancellation,
        out List<(string Name, ITypeSymbol Type, AnonymousObjectMemberDeclaratorSyntax Syntax)> shape
    ) {
        shape = [];
        foreach (var declarator in creation.Initializers) {
            cancellation.ThrowIfCancellationRequested();

            // ⚠ Every initializer must be a chain of plain names. Reordering moves the expressions
            // past one another and the rule may not change the order in which side effects happen.
            if (!RewriteGuards.IsPlainNamePath(declarator.Expression)) {
                return false;
            }

            var name = NameOf(declarator);
            var type = model.GetTypeInfo(declarator.Expression, cancellation).Type;
            if (name is null || type is null || type.TypeKind == TypeKind.Error) {
                return false;
            }

            shape.Add((name, type, declarator));
        }

        return true;
    }

    /// <summary>
    ///     ⚠ Whether the two shapes are the same set of members in a different order — which is
    ///     exactly when they are two runtime types that one edit makes one.
    /// </summary>
    static bool IsReorderOf(
        List<(string Name, ITypeSymbol Type, AnonymousObjectMemberDeclaratorSyntax Syntax)> left,
        List<(string Name, ITypeSymbol Type, AnonymousObjectMemberDeclaratorSyntax Syntax)> right
    ) {
        if (left.Count != right.Count) {
            return false;
        }

        var same = true;
        for (var i = 0; i < left.Count; i++) {
            if (!string.Equals(left[i].Name, right[i].Name, System.StringComparison.Ordinal)) {
                same = false;
                break;
            }
        }

        // Identical order is the case the compiler already unifies; there is nothing to report.
        if (same) {
            return false;
        }

        foreach (var member in left) {
            var counterpart = right.Find(other =>
                string.Equals(other.Name, member.Name, System.StringComparison.Ordinal)
            );

            if (counterpart.Name is null
                || !SymbolEqualityComparer.Default.Equals(counterpart.Type, member.Type)) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     The member's name — written with <c>=</c>, or inferred from a projection like
    ///     <c>new { order.Id }</c>.
    /// </summary>
    static string? NameOf(AnonymousObjectMemberDeclaratorSyntax declarator) =>
        declarator.NameEquals?.Name.Identifier.ValueText
        ?? declarator.Expression switch {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            _ => null
        };
}
