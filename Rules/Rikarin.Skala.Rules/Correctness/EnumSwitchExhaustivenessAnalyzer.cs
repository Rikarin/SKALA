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

/// <summary><c>SK2009</c> — a non-flags enum switch omits declared values and has no catch-all arm.</summary>
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
                start.RegisterSyntaxNodeAction(
                    context => AnalyzeExpression(context, flags),
                    SyntaxKind.SwitchExpression
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

    static void AnalyzeExpression(SyntaxNodeAnalysisContext context, INamedTypeSymbol? flags) {
        var expression = (SwitchExpressionSyntax)context.Node;
        if (expression.Arms.Any(static arm =>
                IsCatchAll(arm.Pattern) || !CanEnumerate(arm.Pattern)
            )) {
            return;
        }

        var handled = expression.Arms.SelectMany(static arm => Expressions(arm.Pattern));
        Report(context, expression.GoverningExpression, expression.SwitchKeyword.GetLocation(), handled, flags);
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

        var missing = type.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(static field => field.HasConstantValue && !field.IsImplicitlyDeclared)
            .GroupBy(static field => Key(field.ConstantValue!), StringComparer.Ordinal)
            .Where(group => !handled.Contains(group.Key))
            .Select(static group => group.First().Name)
            .ToArray();
        if (missing.Length == 0) {
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
