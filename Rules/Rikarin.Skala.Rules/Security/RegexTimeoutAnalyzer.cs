using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Security;

/// <summary>
///     <c>SK5010</c> — a pattern that can backtrack super-linearly, run with no bound on how long it
///     may take.
/// </summary>
/// <remarks>
///     docs/plan/08 § "SK5000 — Security". The vulnerability is a denial of service with no allocation
///     to notice: a nested unbounded quantifier makes the number of ways to decompose a near-match
///     exponential in the input's length, so a few dozen characters can hold a thread for years. The
///     two mitigations are both one argument — a <c>matchTimeout</c>, or
///     <c>RegexOptions.NonBacktracking</c>, which is a linear-time engine — and both are visible in the
///     same expression as the pattern.
///     <para>
///         ⚠ <b>The rule does not fire on "a regex with no timeout".</b> It fires on a pattern it can
///         read and prove dangerous. Sonar's <c>S6444</c> reports every timeout-less regex, and on this
///         repository's two reference trees that would be sixteen findings of which none is a
///         vulnerability — twelve are <c>Assert.Matches(new Regex(@"MaxLevels\s*=\s*4"))</c> in Vixen's
///         own tests, on input the tool itself produced. Reporting those at a security severity is what
///         costs a reviewer their trust in every other security finding.
///     </para>
///     <para>
///         ⚠ <b>The outer quantifier must be unbounded, and Serilog is why.</b>
///         <c>(\.(?&lt;argument&gt;[A-Za-z0-9]*)){0,1}</c> in <c>KeyValuePairSettings</c> is a quantified
///         group whose body carries a quantifier — the shape a naive detector matches — and it cannot
///         blow up, because <c>{0,1}</c> admits at most one iteration. A detector that counted
///         <c>?</c> and <c>{n,m}</c> as outer quantifiers would ship with a false positive already in
///         the corpus.
///     </para>
///     <para>
///         ⚠ <b>The body must be exactly one quantified atom</b> — <c>(a+)+</c>, <c>([a-z]*)*</c>,
///         <c>((ab)+)+</c> — and that is deliberately narrower than "the body contains a quantifier".
///         <c>(abc*)+</c> matches the wider test and is not dangerous: every iteration has to start with
///         <c>ab</c>, so the decomposition is very nearly unique and there is nothing to backtrack
///         through. The cost of the strictness is coverage — <c>^(\w+\s?)*$</c> is a real ReDoS this
///         rule stays silent on — and the alternative is guessing at a security severity.
///     </para>
///     <para>
///         ⚠ A pattern that is not a compile-time constant is silence, and so is a
///         <c>RegexOptions</c> argument that is not. Whether an unknown pattern backtracks is a
///         question about another method, and whether unknown options carry
///         <c>NonBacktracking</c> is a question about the mitigation — reporting either would be
///         guessing. Newtonsoft.Json's <c>Regex.IsMatch(input, patternText, GetRegexOptions(…))</c> is
///         both at once and the rule says nothing about it.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RegexTimeoutAnalyzer : DiagnosticAnalyzer {
    /// <summary><c>RegexOptions.NonBacktracking</c>, which selects an engine that cannot backtrack.</summary>
    const int NonBacktracking = 1024;

    const string Advice =
        "this pattern nests an unbounded quantifier inside another, so an input that nearly matches "
        + "forces the engine through exponentially many decompositions and holds the thread; pass a "
        + "`matchTimeout` to bound it, or add `RegexOptions.NonBacktracking` to remove the backtracking";

    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RegexWithoutTimeout);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var regex = start.Compilation.GetTypeByMetadataName("System.Text.RegularExpressions.Regex");
                if (regex is null) {
                    return;
                }

                var timeSpan = start.Compilation.GetTypeByMetadataName("System.TimeSpan");
                start.RegisterOperationAction(
                    context => Creation(context, regex, timeSpan),
                    OperationKind.ObjectCreation
                );
                start.RegisterOperationAction(
                    context => Invocation(context, regex, timeSpan),
                    OperationKind.Invocation
                );

                var generated = start.Compilation.GetTypeByMetadataName(
                    "System.Text.RegularExpressions.GeneratedRegexAttribute"
                );
                if (generated is not null) {
                    start.RegisterSymbolAction(context => Generated(context, generated), SymbolKind.Method);
                }
            }
        );
    }

    static void Creation(OperationAnalysisContext context, INamedTypeSymbol regex, INamedTypeSymbol? timeSpan) {
        var creation = (IObjectCreationOperation)context.Operation;
        if (creation.Constructor is { } constructor
            && SymbolEqualityComparer.Default.Equals(constructor.ContainingType.OriginalDefinition, regex)) {
            Check(context, creation.Arguments, timeSpan, creation.Syntax.GetLocation());
        }
    }

    /// <summary>
    ///     ⚠ Static only. An instance <c>IsMatch</c> inherits the timeout its constructor was given, so
    ///     the construction site is the one place the fact lives; reporting the call as well would report
    ///     one regex many times and would report a correctly-built one every time it is used.
    /// </summary>
    static void Invocation(OperationAnalysisContext context, INamedTypeSymbol regex, INamedTypeSymbol? timeSpan) {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.TargetMethod.IsStatic
            && SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType.OriginalDefinition, regex)) {
            Check(context, invocation.Arguments, timeSpan, invocation.Syntax.GetLocation());
        }
    }

    static void Generated(SymbolAnalysisContext context, INamedTypeSymbol attributeType) {
        foreach (var attribute in context.Symbol.GetAttributes()) {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass?.OriginalDefinition, attributeType)) {
                continue;
            }

            var arguments = attribute.ConstructorArguments;
            if (arguments.Length == 0 || arguments[0].Value is not string pattern) {
                continue;
            }

            // ⚠ The third parameter is `matchTimeoutMilliseconds` on one overload and `cultureName` on
            // another, so the count does not decide it and the type does.
            if (arguments.Length >= 3 && arguments[2].Value is int) {
                continue;
            }

            if (arguments.Length >= 2 && arguments[1].Value is int options && (options & NonBacktracking) != 0) {
                continue;
            }

            if (!Backtracks(pattern)
                || attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) is not { } syntax) {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Descriptor, syntax.GetLocation(), Advice));
        }
    }

    static void Check(
        OperationAnalysisContext context,
        ImmutableArray<IArgumentOperation> arguments,
        INamedTypeSymbol? timeSpan,
        Location location
    ) {
        string? pattern = null;
        var bounded = false;
        var unknownOptions = false;

        foreach (var argument in arguments) {
            if (argument.Parameter is not { } parameter || argument.ArgumentKind == ArgumentKind.DefaultValue) {
                continue;
            }

            if (timeSpan is not null
                && SymbolEqualityComparer.Default.Equals(parameter.Type.OriginalDefinition, timeSpan)) {
                bounded = true;
            } else if (parameter.Name == "pattern" && parameter.Type.SpecialType == SpecialType.System_String) {
                if (argument.Value.ConstantValue is { HasValue: true, Value: string text }) {
                    pattern = text;
                }
            } else if (parameter.Type.Name == "RegexOptions") {
                if (argument.Value.ConstantValue is not { HasValue: true, Value: int options }) {
                    unknownOptions = true;
                } else if ((options & NonBacktracking) != 0) {
                    bounded = true;
                }
            }
        }

        if (!bounded && !unknownOptions && pattern is not null && Backtracks(pattern)) {
            context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, Advice));
        }
    }

    /// <summary>
    ///     Whether the pattern contains a group under an <em>unbounded</em> quantifier whose body is
    ///     exactly one atom carrying its own quantifier.
    /// </summary>
    /// <remarks>
    ///     ⚠ Fails closed in every direction it can: an unbalanced pattern, a construct the scanner does
    ///     not model, or a body of more than one atom all return <c>false</c>. A miss is silence; a wrong
    ///     answer here is a security finding on correct code.
    /// </remarks>
    static bool Backtracks(string pattern) {
        for (var i = 0; i < pattern.Length; i++) {
            var current = pattern[i];
            if (current == '\\') {
                i++;
                continue;
            }

            if (current == '[') {
                i = ClassEnd(pattern, i);
                continue;
            }

            if (current != '(') {
                continue;
            }

            var body = BodyStart(pattern, i);
            var close = MatchingParen(pattern, i);
            if (body < 0 || close < 0) {
                continue;
            }

            if (Unbounded(pattern, close + 1) && SingleQuantifiedAtom(pattern, body, close)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Where the group's body starts, or <c>-1</c> for a construct this rule does not reason about.
    /// </summary>
    /// <remarks>
    ///     ⚠ Lookarounds, atomic groups, inline options and comments are all excluded rather than
    ///     guessed at. An atomic group <c>(?&gt;…)</c> is in particular the <em>mitigation</em> — it
    ///     forbids backtracking into the group — so reporting it would report the fix.
    /// </remarks>
    static int BodyStart(string pattern, int open) {
        if (open + 1 >= pattern.Length || pattern[open + 1] != '?') {
            return open + 1;
        }

        if (open + 2 < pattern.Length && pattern[open + 2] == ':') {
            return open + 3;
        }

        // `(?<name>` is a capture; `(?<=` and `(?<!` are lookbehind.
        if (open + 3 < pattern.Length && pattern[open + 2] == '<' && pattern[open + 3] != '=' && pattern[open + 3] != '!') {
            var end = pattern.IndexOf('>', open + 3);
            return end < 0 ? -1 : end + 1;
        }

        return -1;
    }

    /// <summary>Whether an unbounded quantifier — <c>*</c>, <c>+</c> or <c>{n,}</c> — starts at <paramref name="at" />.</summary>
    static bool Unbounded(string pattern, int at) {
        if (at >= pattern.Length) {
            return false;
        }

        if (pattern[at] == '*' || pattern[at] == '+') {
            return true;
        }

        if (pattern[at] != '{') {
            return false;
        }

        var close = pattern.IndexOf('}', at);
        if (close < 0) {
            return false;
        }

        // `{n,}` is unbounded; `{n}` and `{n,m}` are not.
        var comma = pattern.IndexOf(',', at);
        return comma > 0 && comma < close && comma == close - 1;
    }

    /// <summary>Whether <c>[start, end)</c> is one atom followed by a quantifier and nothing else.</summary>
    static bool SingleQuantifiedAtom(string pattern, int start, int end) {
        var after = AtomEnd(pattern, start, end);
        if (after < 0 || after >= end) {
            return false;
        }

        var quantifier = pattern[after];
        if (quantifier == '*' || quantifier == '+' || quantifier == '?') {
            after++;
        } else if (quantifier == '{') {
            var close = pattern.IndexOf('}', after);
            if (close < 0 || close >= end) {
                return false;
            }

            after = close + 1;
        } else {
            return false;
        }

        // A lazy or possessive marker still backtracks (or, for `+`, is not .NET syntax at all).
        if (after < end && pattern[after] == '?') {
            after++;
        }

        return after == end;
    }

    /// <summary>Where the atom starting at <paramref name="at" /> ends, or <c>-1</c> if there is none.</summary>
    static int AtomEnd(string pattern, int at, int end) {
        if (at >= end) {
            return -1;
        }

        switch (pattern[at]) {
            case '\\':
                // `\p{L}` and `\P{L}` carry a braced name; every other escape is two characters.
                if (at + 2 < end && (pattern[at + 1] == 'p' || pattern[at + 1] == 'P') && pattern[at + 2] == '{') {
                    var close = pattern.IndexOf('}', at + 2);
                    return close < 0 || close >= end ? -1 : close + 1;
                }

                return at + 2 > end ? -1 : at + 2;

            case '[':
                var classEnd = ClassEnd(pattern, at);
                return classEnd >= end ? -1 : classEnd + 1;

            case '(':
                var paren = MatchingParen(pattern, at);
                return paren < 0 || paren >= end ? -1 : paren + 1;

            // Not an atom: a quantifier with nothing to quantify, an alternation, or a stray close.
            case '*':
            case '+':
            case '?':
            case '|':
            case ')':
                return -1;

            default:
                return at + 1;
        }
    }

    /// <summary>The index of the <c>)</c> closing the group opened at <paramref name="open" />, or <c>-1</c>.</summary>
    static int MatchingParen(string pattern, int open) {
        var depth = 0;
        for (var i = open; i < pattern.Length; i++) {
            switch (pattern[i]) {
                case '\\':
                    i++;
                    break;

                case '[':
                    i = ClassEnd(pattern, i);
                    break;

                case '(':
                    depth++;
                    break;

                case ')':
                    depth--;
                    if (depth == 0) {
                        return i;
                    }

                    break;
            }
        }

        return -1;
    }

    /// <summary>
    ///     The index of the <c>]</c> closing the character class opened at <paramref name="open" />, or
    ///     the last index when the class is unterminated.
    /// </summary>
    /// <remarks>
    ///     ⚠ A <c>]</c> immediately after <c>[</c> or <c>[^</c> is a literal, not the terminator. Getting
    ///     this wrong makes the scanner read <c>(</c> and <c>*</c> inside a class as structure.
    /// </remarks>
    static int ClassEnd(string pattern, int open) {
        var i = open + 1;
        if (i < pattern.Length && pattern[i] == '^') {
            i++;
        }

        if (i < pattern.Length && pattern[i] == ']') {
            i++;
        }

        for (; i < pattern.Length; i++) {
            if (pattern[i] == '\\') {
                i++;
            } else if (pattern[i] == ']') {
                return i;
            }
        }

        return pattern.Length - 1;
    }
}
