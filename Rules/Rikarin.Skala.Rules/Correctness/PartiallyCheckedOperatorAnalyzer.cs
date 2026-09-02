using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2222</c> — the type declares a <c>checked</c> operator for some of its arithmetic and not
///     for the rest, so <c>checked</c> traps on one operator of the same type and wraps on another.
/// </summary>
/// <remarks>
///     ⚠ <b>"No checked operator" is an observation; "checked on some and not others" is a defect, and
///     the difference is the whole specification of this rule.</b> A type that declares no
///     <c>checked</c> operator at all has simply not opted into C# 11's user-defined checked
///     arithmetic, and there is nothing to report: <c>checked</c> around it means what it has always
///     meant. But a type that declares <c>operator checked +</c> and not <c>operator checked -</c> has
///     stated, in its own source, that overflow on this type is meant to trap — and then
///     <c>checked(a - b)</c> silently wraps while <c>checked(a + b)</c> next to it throws. The evidence
///     for the author's intent comes from the type itself, so the rule never has to guess whether
///     overflow matters here.
///     <para>
///         ⚠ <b>The eight operators that <em>have</em> a checked form are listed, and the list was
///         measured against the compiler rather than remembered.</b> Binary <c>+</c>, <c>-</c>,
///         <c>*</c>, <c>/</c>; unary <c>-</c>; <c>++</c>; <c>--</c>; and the <b>explicit</b> conversion.
///         Every other operator is rejected outright: <c>CS9023</c> for unary <c>+</c>, <c>%</c>,
///         <c>&amp;</c>, <c>&lt;&lt;</c> and <c>==</c>, and <c>CS9024</c> for an <c>implicit</c>
///         conversion. Naming one of those in this list would be asking for a declaration the language
///         does not allow.
///     </para>
///     <para>
///         ⚠ <b>The opposite direction is already a compiler error and is not restated here.</b>
///         <c>operator checked +</c> with no matching unchecked <c>+</c> is <c>CS9025</c>. Only the
///         direction the compiler is silent about is this rule's subject.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PartiallyCheckedOperatorAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.PartiallyCheckedOperatorSet);

    /// <summary>The unchecked metadata name of each operator that has a checked form, and its pair.</summary>
    static readonly Dictionary<string, (string Checked, string Spelling)> Checkable = new(System.StringComparer.Ordinal) {
        ["op_Addition"] = ("op_CheckedAddition", "+"),
        ["op_Subtraction"] = ("op_CheckedSubtraction", "-"),
        ["op_Multiply"] = ("op_CheckedMultiply", "*"),
        ["op_Division"] = ("op_CheckedDivision", "/"),
        ["op_UnaryNegation"] = ("op_CheckedUnaryNegation", "-"),
        ["op_Increment"] = ("op_CheckedIncrement", "++"),
        ["op_Decrement"] = ("op_CheckedDecrement", "--"),
        ["op_Explicit"] = ("op_CheckedExplicit", "explicit conversion")
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    static void Analyze(SymbolAnalysisContext context) {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!SkalaRule.MeetsLanguageVersion(context.Compilation, RuleCatalog.Get(RuleIds.PartiallyCheckedOperatorSet).LanguageVersion)) {
            return;
        }

        var operators = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(static method => method.MethodKind is MethodKind.UserDefinedOperator or MethodKind.Conversion)
            .ToList();

        var declaredChecked = operators.Where(static method => method.Name.StartsWith("op_Checked", System.StringComparison.Ordinal))
            .ToList();

        // The whole predicate: the type has to have opted in somewhere, or there is no defect here.
        if (declaredChecked.Count == 0) {
            return;
        }

        foreach (var method in operators) {
            if (!Checkable.TryGetValue(method.Name, out var pair)) {
                continue;
            }

            if (declaredChecked.Any(candidate => Matches(candidate, pair.Checked, method))) {
                continue;
            }

            foreach (var location in method.Locations.Where(static location => location.IsInSource)) {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        location,
                        "`"
                        + type.Name
                        + "` declares `operator checked "
                        + Spelling(declaredChecked)
                        + "` and no `operator checked "
                        + pair.Spelling
                        + "`, so overflow traps on one and wraps on the other inside the same `checked` block"
                    )
                );
                break;
            }
        }
    }

    /// <summary>
    ///     Whether a declared checked operator is <em>this</em> operator's counterpart.
    /// </summary>
    /// <remarks>
    ///     ⚠ The parameter types must match, not merely the name. A type may overload <c>+</c> several
    ///     times — <c>Money + Money</c> and <c>Money + long</c> — and a checked form of one of them
    ///     says nothing about the other. The return type is compared as well, because that is what
    ///     distinguishes two explicit conversions from the same source type.
    /// </remarks>
    static bool Matches(IMethodSymbol candidate, string name, IMethodSymbol unchecked_) {
        if (!string.Equals(candidate.Name, name, System.StringComparison.Ordinal)
            || candidate.Parameters.Length != unchecked_.Parameters.Length
            || !SymbolEqualityComparer.Default.Equals(candidate.ReturnType, unchecked_.ReturnType)) {
            return false;
        }

        for (var i = 0; i < candidate.Parameters.Length; i++) {
            if (!SymbolEqualityComparer.Default.Equals(candidate.Parameters[i].Type, unchecked_.Parameters[i].Type)) {
                return false;
            }
        }

        return true;
    }

    /// <summary>How to spell the checked operator the type <em>did</em> declare, for the message.</summary>
    /// <remarks>
    ///     ⚠ <b>Reads the list rather than indexing it, and the reason is a crash this actually had.</b>
    ///     The earlier form was <c>declaredChecked[0]</c>, safe only because the opt-in predicate ten
    ///     lines above guarantees the list is not empty. Sabotaging that predicate to measure how noisy
    ///     the bare shape is did not produce the flood of findings it was meant to — it produced
    ///     <c>ArgumentOutOfRangeException</c>, reported as <c>AD0001</c> on seven unrelated fixtures,
    ///     and <em>a crashed analyzer declines every negative fixture it was supposed to decline</em>.
    ///     A message helper that cannot throw is worth more than the one character it saves.
    /// </remarks>
    static string Spelling(List<IMethodSymbol> declared) {
        foreach (var method in declared) {
            foreach (var pair in Checkable.Values) {
                if (string.Equals(pair.Checked, method.Name, System.StringComparison.Ordinal)) {
                    return pair.Spelling;
                }
            }
        }

        return "operator";
    }
}
