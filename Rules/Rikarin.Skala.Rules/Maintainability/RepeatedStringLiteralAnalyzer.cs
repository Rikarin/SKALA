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

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     <c>SK7083</c>: one string literal written more times in a file than the threshold allows.
/// </summary>
/// <remarks>
///     The same literal in several places is the same rename waiting to be done incompletely — the
///     argument <c>nameof</c> makes for member names, applied to keys, formats and messages.
///     <para>
///         ⚠ <b>Two options, not one, and the second is what makes the rule usable.</b> A repeat count on
///         its own reports <c>", "</c>, <c>"/"</c> and <c>"true"</c> before it reports anything worth
///         extracting. <c>dotnet_code_quality.SK7083.minimum_length</c> holds the floor, and a literal
///         must also contain a letter: a repeated separator or punctuation run is not a name anybody was
///         going to give.
///     </para>
///     <para>
///         ⚠ <b>Per file, and per file on purpose.</b> The scope where a repeat is visible and a
///         <c>const</c> is an obvious repair is the one a reader has open. Counting per compilation would
///         report a literal shared by two unrelated files, where the repair is a shared constant nobody
///         has a place for and the finding points at neither site.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RepeatedStringLiteralAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RepeatedStringLiteral);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(Analyze);
    }

    static void Analyze(SyntaxTreeAnalysisContext context) {
        var thresholds = MetricThresholds.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Tree));
        var minimum = thresholds.LiteralLength;
        var allowed = thresholds.LiteralRepeats;

        var occurrences = new Dictionary<string, List<LiteralExpressionSyntax>>(StringComparer.Ordinal);
        var root = context.Tree.GetRoot(context.CancellationToken);
        foreach (var node in root.DescendantNodes()) {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (node is not LiteralExpressionSyntax literal
                || !literal.IsKind(SyntaxKind.StringLiteralExpression)
                || literal.Token.Value is not string value
                || !IsWorthNaming(value, minimum)
                || IsItsOwnName(literal)) {
                continue;
            }

            if (!occurrences.TryGetValue(value, out var sites)) {
                occurrences[value] = sites = [];
            }

            sites.Add(literal);
        }

        // ⚠ Ordered by where the literal first appears, not by the dictionary's bucket order, so two
        // runs over the same file produce the same report in the same sequence.
        foreach (var pair in occurrences.OrderBy(static entry => entry.Value[0].SpanStart)) {
            var value = pair.Key;
            var sites = pair.Value;
            if (sites.Count <= allowed) {
                continue;
            }

            // ⚠ One finding per literal, at the first occurrence. One per *site* would report the
            // same decision as many times as it was made, and the reader has to look at all of them
            // anyway to make the change.
            var properties = ImmutableDictionary<string, string?>.Empty.Add(
                MemberMetrics.ValueKey,
                sites.Count.ToString(CultureInfo.InvariantCulture)
            );

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    sites[0].GetLocation(),
                    properties,
                    "The literal `\""
                    + value
                    + "\"` is written "
                    + sites.Count.ToString(CultureInfo.InvariantCulture)
                    + " times in this file, over the threshold of "
                    + allowed.ToString(CultureInfo.InvariantCulture)
                )
            );
        }
    }

    /// <summary>
    ///     Whether a literal is long enough and word-like enough that a name would be an improvement.
    /// </summary>
    /// <remarks>
    ///     ⚠ The letter test is not a refinement of the length test. <c>"----------"</c> and
    ///     <c>" | "</c> pass any length floor anybody would set and neither is a name waiting to be
    ///     given; what makes a repeated literal worth extracting is that it *says* something, and in C#
    ///     that means it has letters in it.
    /// </remarks>
    static bool IsWorthNaming(string value, int minimum) {
        if (value.Length < minimum) {
            return false;
        }

        foreach (var character in value) {
            if (char.IsLetter(character)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether this occurrence is the extraction the rule would ask for, or a place no name can go.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>A <c>const</c> initialiser is the repair, not the problem.</b> Counting it means the rule
    ///     still fires after somebody has done exactly what it asked, which is the fastest way to teach
    ///     people that a rule is noise.
    ///     <para>
    ///         ⚠ <b>An attribute argument is excluded.</b> It has to be a compile-time constant, so the
    ///         only extraction available is a <c>const</c> that the attribute then names — and the ones
    ///         that actually repeat are test display names, obsolescence messages and route templates,
    ///         where the string being written out in full at the declaration is the point.
    ///     </para>
    /// </remarks>
    static bool IsItsOwnName(LiteralExpressionSyntax literal) {
        for (var node = literal.Parent; node is not null; node = node.Parent) {
            switch (node) {
                case AttributeSyntax:
                    return true;
                case VariableDeclaratorSyntax { Parent.Parent: BaseFieldDeclarationSyntax field }
                    when field.Modifiers.Any(SyntaxKind.ConstKeyword):
                    return true;
                case VariableDeclaratorSyntax { Parent.Parent: LocalDeclarationStatementSyntax local }
                    when local.IsConst:
                    return true;
                case EnumMemberDeclarationSyntax:
                case MemberDeclarationSyntax:
                case StatementSyntax:
                    return false;
            }
        }

        return false;
    }
}
