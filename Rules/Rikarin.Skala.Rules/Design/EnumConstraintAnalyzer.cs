using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Design;

/// <summary><c>SK6020</c> — <c>where T : Enum</c> with no <c>struct</c> beside it.</summary>
/// <remarks>
///     ⚠ The proposal (issue #261) says the bare constraint admits <c>Nullable&lt;TEnum&gt;</c>. It does
///     not: the compiler answers CS0312 for <c>Name&lt;Colour?&gt;(…)</c>, which was measured before this
///     was written. The one type argument <c>struct</c> excludes is <c>System.Enum</c> itself, and that
///     is enough — under the bare constraint <c>T</c> is not known to be a value type, so
///     <c>default(T)</c> is <c>null</c>, every use boxes, and none of the BCL's generic enum APIs
///     (all declared <c>where TEnum : struct, Enum</c>) can be called.
///     <para>
///         Semantic because <c>Enum</c>, <c>System.Enum</c>, an alias and a user type named <c>Enum</c>
///         are different questions, and only one of them is this rule's.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EnumConstraintAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.EnumConstraintWithoutStruct);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.TypeParameterConstraintClause);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var clause = (TypeParameterConstraintClauseSyntax)context.Node;

        // ⚠ The fix inserts one token at a position rather than replacing one, so a directive
        // anywhere in the clause means the position the fix names may not be the position every
        // branch compiles. Same refusal as SK6003's.
        if (clause.Constraints.Count == 0 || clause.ContainsDirectives) {
            return;
        }

        TypeConstraintSyntax? bound = null;
        foreach (var constraint in clause.Constraints) {
            switch (constraint) {
                // `class` and `struct`. `struct` already says it, and `where T : class, Enum` —
                // which the language does allow — is a deliberate statement that `T` is a
                // reference type, so the one thing `struct` would add is a contradiction.
                case ClassOrStructConstraintSyntax:

                // `new()`. CS0451 forbids it beside `struct` — which implies it — so the repair
                // needs a second edit, and this rule makes one.
                case ConstructorConstraintSyntax:

                // `default`, which only appears on an override and cannot take `struct`.
                case DefaultConstraintSyntax:
                    return;

                case TypeConstraintSyntax type:
                    if (IsKeywordConstraint(type)) {
                        // `unmanaged` is already stricter, and `notnull` cannot be combined with
                        // `struct` (CS8716).
                        return;
                    }

                    if (type.Type is not NullableTypeSyntax
                        && context.SemanticModel.GetTypeInfo(type.Type, context.CancellationToken).Type
                        is { SpecialType: SpecialType.System_Enum }) {
                        bound = type;
                    }

                    break;
            }
        }

        // ⚠ CS0406 — "the class type constraint 'Enum' must come before any other constraints" —
        // means a legal clause always puts the `Enum` first among the type constraints, so the
        // token goes immediately in front of it and nowhere else. Requiring that rather than
        // assuming it keeps the insertion point provable from this one node.
        if (bound is null || bound != clause.Constraints[0]) {
            return;
        }

        var insertion = bound.SpanStart;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                bound.GetLocation(),
                FixEdits.Pack((new TextSpan(insertion, 0), "struct, ")),
                "`where "
                + clause.Name.Identifier.ValueText
                + " : Enum` still admits `System.Enum` itself, so `"
                + clause.Name.Identifier.ValueText
                + "` is not known to be a value type; write `struct, Enum`"
            )
        );
    }

    /// <summary>
    ///     Whether a constraint is one of the contextual keywords the parser models as a type.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>unmanaged</c> and <c>notnull</c> are <see cref="TypeConstraintSyntax" /> holding a bare
    ///     identifier, not their own node kinds — so a rule that only looks at node kinds treats them as
    ///     ordinary type constraints and offers a fix that does not compile.
    /// </remarks>
    static bool IsKeywordConstraint(TypeConstraintSyntax constraint) =>
        constraint.Type is IdentifierNameSyntax name
        && name.Identifier.ValueText is "unmanaged" or "notnull";
}
