using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     What <c>SK2009</c> has to say about one <c>switch</c> statement, asked as a question rather than
///     reported as a finding.
/// </summary>
/// <remarks>
///     ⚠ <b>Shared rather than duplicated because the answers have to agree</b>, for the same reason
///     <see cref="Async.AsyncContext" /> is shared. <c>SK2009</c> reads a <c>default:</c> section as the
///     catch-all that legitimises a non-exhaustive enum switch; <c>SK0240</c> reads an <em>empty</em>
///     one as dead control flow and offers to delete it. Both are defensible alone and together they
///     are a fix loop: taking <c>SK0240</c>'s fix hands the author an <c>SK2009</c> they did not have
///     ([#321]). The contradiction is settled by <c>SK0240</c> asking this type, before it reports,
///     whether the section it wants to delete is the only thing keeping <c>SK2009</c> quiet.
///     <para>
///         ⚠ The answer is <em>opportunistic</em> for <c>SK0240</c>'s purposes. That rule is
///         <c>scope: Syntax</c> and runs in loose mode, where an enum arriving from an unreferenced
///         assembly does not resolve — and there the gap is empty, so <c>SK0240</c> reports exactly as
///         it did before. That is not a hole so much as loose mode's own bargain: <c>SK2009</c> is
///         <c>requiresSemantics</c> and is not running there either, so the two still agree about the
///         file in front of them.
///     </para>
/// </remarks>
internal static class EnumSwitchCoverage {
    /// <summary>The governing enum and the members no label covers, or null when there is nothing to report.</summary>
    /// <param name="ignored">
    ///     A section to answer as though it were already deleted, which is how <c>SK0240</c> asks what
    ///     its own fix would produce. Null asks about the statement as written.
    /// </param>
    public static (INamedTypeSymbol Type, string[] Missing)? Gap(
        SemanticModel model,
        SwitchStatementSyntax statement,
        INamedTypeSymbol? flags,
        SwitchSectionSyntax? ignored,
        CancellationToken cancellation
    ) {
        var labels = statement.Sections
            .Where(section => !ReferenceEquals(section, ignored))
            .SelectMany(static section => section.Labels)
            .ToArray();

        if (labels.Any(IsCatchAll)
            || labels.OfType<CasePatternSwitchLabelSyntax>()
                .Any(static label =>
                    !CanEnumerate(label.Pattern)
                )) {
            return null;
        }

        if (model.GetTypeInfo(statement.Expression, cancellation).Type
            is not INamedTypeSymbol { TypeKind: TypeKind.Enum } type
            || flags is not null
            && type.GetAttributes()
                .Any(attribute =>
                    SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, flags)
                )) {
            return null;
        }

        var handled = new HashSet<string>(StringComparer.Ordinal);
        foreach (var expression in labels.SelectMany(Expressions)) {
            var value = model.GetConstantValue(expression, cancellation);
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
            return null;
        }

        return (type, missing);
    }

    /// <summary>The message body <c>SK2009</c> reports, which is also what <c>SK0240</c> stands down for.</summary>
    public static string Describe(INamedTypeSymbol type, string[] missing) =>
        "switch over `"
        + type.Name
        + "` omits "
        + string.Join(", ", missing.Take(5).Select(static name => "`" + name + "`"))
        + (missing.Length > 5
                ? " and " + (missing.Length - 5).ToString(CultureInfo.InvariantCulture) + " more"
                : string.Empty);

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
