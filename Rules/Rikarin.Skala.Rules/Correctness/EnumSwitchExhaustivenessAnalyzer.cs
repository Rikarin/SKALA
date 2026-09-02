using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary><c>SK2009</c> — a non-flags enum switch statement omits declared values and has no catch-all.</summary>
/// <remarks>
///     <para>
///         ⚠ <b>The switch <em>expression</em> is the compiler's, and this rule does not look at
///         one.</b> ADR-008 is host, never rebuild: <c>CS8509</c> ("does not handle all possible values
///         … the pattern 'K.C' is not covered") and <c>CS8524</c> (the undeclared-value half) are on by
///         default and name the missing member. Probed on a scratch project rather than recalled:
///         <c>k switch { K.A => 1, K.B => 2 }</c> draws <c>CS8509</c>, and the same switch written as a
///         statement draws nothing at all. The statement is the whole of what is left to say.
///     </para>
///     <para>
///         ⚠ <b>A statement that covers a minority of the enum is a filter, not a forgotten case</b>
///         (#280). A <c>switch</c> statement is under no obligation to be exhaustive — falling out of it
///         continues at the next statement, and that is the designed path for
///         <c>switch (modifier.Kind()) { case SyntaxKind.AsyncKeyword: return false; }</c>. The
///         boundary is <c>missing &lt;= handled</c>: a switch that already lists at least half the
///         declared values is visibly attempting exhaustiveness and forgot some; one listing three of
///         <c>SyntaxKind</c>'s 570 is selecting.
///     </para>
///     <para>
///         ⚠ Measured on Skala's own tree, where the rule made <b>14</b> findings and <b>13</b> were
///         false: every <c>SyntaxKind</c> and <c>SpecialType</c> filter, and — against #280's own
///         reading — the <c>JsonValueKind</c> one too, which recurses into <c>Object</c> and
///         <c>Array</c> and correctly ignores the six scalar kinds. The single survivor is
///         <c>OptionValueKind</c>, four of five members handled and <c>String</c> forgotten.
///     </para>
///     <para>
///         The recall this costs is real and unmeasured: a ten-member enum with three cases handled and
///         seven genuinely forgotten is now silent. The two candidates that would have kept it — a
///         member-count threshold, and "the enum is declared in this compilation" — were rejected on
///         #280, the second because <c>OptionValueKind</c> and <c>SyntaxKind</c> both arrive from
///         referenced assemblies and land on the same side of it.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EnumSwitchExhaustivenessAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.EnumSwitchMissingMembers);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var flags = start.Compilation.GetTypeByMetadataName("System.FlagsAttribute");
                start.RegisterSyntaxNodeAction(
                    context => AnalyzeStatement(context, flags),
                    SyntaxKind.SwitchStatement
                );
            }
        );
    }

    static void AnalyzeStatement(SyntaxNodeAnalysisContext context, INamedTypeSymbol? flags) {
        var statement = (SwitchStatementSyntax)context.Node;
        var labels = statement.Sections.SelectMany(static section => section.Labels).ToArray();
        if (labels.Any(IsCatchAll)
            || labels.OfType<CasePatternSwitchLabelSyntax>()
                .Any(static label =>
                    !CanEnumerate(label.Pattern)
                )) {
            return;
        }

        var expressions = labels.SelectMany(Expressions);
        Report(context, statement.Expression, statement.SwitchKeyword.GetLocation(), expressions, flags);
    }

    static void Report(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax governing,
        Location location,
        IEnumerable<ExpressionSyntax> handledExpressions,
        INamedTypeSymbol? flags
    ) {
        if (context.SemanticModel.GetTypeInfo(governing, context.CancellationToken).Type
            is not INamedTypeSymbol { TypeKind: TypeKind.Enum } type
            || flags is not null
            && type.GetAttributes()
                .Any(attribute =>
                    SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, flags)
                )) {
            return;
        }

        var handled = new HashSet<string>(StringComparer.Ordinal);
        foreach (var expression in handledExpressions) {
            var value = context.SemanticModel.GetConstantValue(expression, context.CancellationToken);
            if (value.HasValue && value.Value is not null) {
                handled.Add(Key(value.Value));
            }
        }

        var values = type.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(static field => field.HasConstantValue && !field.IsImplicitlyDeclared)
            .GroupBy(static field => Key(field.ConstantValue!), StringComparer.Ordinal)
            .ToArray();

        var missing = values
            .Where(group => !handled.Contains(group.Key))
            .Select(static group => group.First().Name)
            .ToArray();

        // ⚠ `missing > values.Length - missing` is the filter test, and it is the whole of #280's fix
        // for the statement form. Distinct *values* rather than members, so `{ First = 0, AlsoFirst = 0 }`
        // counts once on both sides and an alias cannot tip the comparison on its own.
        if (missing.Length == 0 || missing.Length > values.Length - missing.Length) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                location,
                "switch over `"
                + type.Name
                + "` omits "
                + string.Join(", ", missing.Take(5).Select(static name => "`" + name + "`"))
                + (missing.Length > 5
                        ? " and " + (missing.Length - 5).ToString(CultureInfo.InvariantCulture) + " more"
                        : string.Empty)
            )
        );
    }

    static string Key(object value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    static bool IsCatchAll(SwitchLabelSyntax label) =>
        label is DefaultSwitchLabelSyntax
        || label is CasePatternSwitchLabelSyntax { Pattern: var pattern }
        && IsCatchAll(pattern);

    static bool IsCatchAll(PatternSyntax pattern) =>
        pattern is DiscardPatternSyntax or VarPatternSyntax or DeclarationPatternSyntax
        || pattern is ParenthesizedPatternSyntax parenthesized
        && IsCatchAll(parenthesized.Pattern);

    static bool CanEnumerate(PatternSyntax pattern) =>
        pattern is ConstantPatternSyntax
        || pattern is ParenthesizedPatternSyntax parenthesized
        && CanEnumerate(parenthesized.Pattern)
        || pattern is BinaryPatternSyntax binary
        && binary.IsKind(SyntaxKind.OrPattern)
        && CanEnumerate(binary.Left)
        && CanEnumerate(binary.Right);

    static IEnumerable<ExpressionSyntax> Expressions(SwitchLabelSyntax label) =>
        label switch {
            CaseSwitchLabelSyntax simple => [simple.Value],
            CasePatternSwitchLabelSyntax pattern => Expressions(pattern.Pattern),
            _ => []
        };

    static IEnumerable<ExpressionSyntax> Expressions(PatternSyntax pattern) =>
        pattern switch {
            ConstantPatternSyntax constant => [constant.Expression],
            BinaryPatternSyntax binary when binary.IsKind(SyntaxKind.OrPattern) =>
                Expressions(binary.Left).Concat(Expressions(binary.Right)),
            ParenthesizedPatternSyntax parenthesized => Expressions(parenthesized.Pattern),
            _ => []
        };
}
