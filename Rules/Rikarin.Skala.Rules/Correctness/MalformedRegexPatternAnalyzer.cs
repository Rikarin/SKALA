using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2241</c> — a constant pattern that <c>Regex</c> will refuse to parse.
/// </summary>
/// <remarks>
///     <para>
///         <c>new Regex("(")</c> compiles, ships, and throws the first time the line runs. The pattern
///         is a constant, so whether it parses was settled when it was typed; the only question left is
///         whether a test happens to reach the line before a user does.
///     </para>
///     <para>
///         ⚠ <b>The oracle is <c>Regex</c> itself.</b> The analyzer constructs the pattern with the same
///         options the call passes and reports what the constructor threw, so the rule cannot disagree
///         with the runtime whose behaviour it is predicting, and it needs no regex parser of its own.
///         Construction parses; it does not match. No pattern can make this backtrack.
///     </para>
///     <para>
///         ⚠
///         <b>
///             <c>SYSLIB1042</c> already owns the <c>[GeneratedRegex]</c> half, as an on-by-default
///             compiler error
///         </b> — measured on a probe outside this repository with empty
///         <c>Directory.Build.props</c>/<c>.targets</c> above it, in the SDK's pristine default state.
///         This rule registers on invocations and object creations only, so the attribute form is out of
///         reach by construction rather than by filter.
///     </para>
///     <para>
///         ⚠ <b><c>SYSLIB1045</c> is orthogonal, not overlapping.</b> It is <c>Hidden</c> by default —
///         silent even at <c>AnalysisMode=All</c> — and when forced on it fires identically on malformed
///         and well-formed patterns, because it says "use <c>GeneratedRegexAttribute</c>" and never reads
///         the pattern.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MalformedRegexPatternAnalyzer : DiagnosticAnalyzer {
    /// <summary>
    ///     ⚠ <c>RegexOptions.Compiled</c>, stripped before the probe. It emits IL, which would make the
    ///     analyzer pay a JIT for every pattern it checks, and it cannot change whether one parses.
    /// </summary>
    const int Compiled = 8;

    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.MalformedRegexPattern);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var regex = start.Compilation.GetTypeByMetadataName("System.Text.RegularExpressions.Regex");
                if (regex is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    nodeContext => Analyze(nodeContext, regex),
                    SyntaxKind.InvocationExpression,
                    SyntaxKind.ObjectCreationExpression,
                    SyntaxKind.ImplicitObjectCreationExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol regex) {
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        var arguments = context.Node switch {
            InvocationExpressionSyntax invocation => invocation.ArgumentList,
            BaseObjectCreationExpressionSyntax creation => creation.ArgumentList,
            _ => null
        };
        if (arguments is not { Arguments.Count: > 0 }) {
            return;
        }

        // The member has to be one of `Regex`'s own. A user method with a `pattern` parameter is not
        // this rule's subject, and nothing else says so.
        if (model.GetSymbolInfo(context.Node, cancellation).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, regex)) {
            return;
        }

        if (Argument(arguments, method, "pattern") is not { } pattern
            || model.GetConstantValue(pattern, cancellation) is not { HasValue: true, Value: string text }) {
            return;
        }

        // ⚠ The options decide what parses — `IgnorePatternWhitespace` and `ECMAScript` both change
        // which patterns are legal — so a call whose options cannot be folded to a constant declines
        // rather than asking `Regex` a question the program does not ask it.
        var options = 0;
        if (Argument(arguments, method, "options") is { } given) {
            var constant = model.GetConstantValue(given, cancellation);
            if (!constant.HasValue || constant.Value is null) {
                return;
            }

            try {
                options = Convert.ToInt32(constant.Value, System.Globalization.CultureInfo.InvariantCulture);
            } catch (SystemException) {
                return;
            }
        }

        if (Rejects(text, options) is not { } reason) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                pattern.GetLocation(),
                "`Regex` will reject this pattern the first time this line runs: " + reason
            )
        );
    }

    /// <summary>
    ///     What <c>Regex</c> says is wrong with the pattern, or null where it accepts it.
    /// </summary>
    /// <remarks>
    ///     ⚠ <see cref="ArgumentOutOfRangeException" /> and <see cref="ArgumentNullException" /> are
    ///     declined rather than reported. Both derive from <see cref="ArgumentException" /> and neither
    ///     is a statement about the pattern: the first is what an options value this analyzer's host does
    ///     not recognise produces, and reporting it would turn a difference between two framework
    ///     versions into a finding about the user's code.
    /// </remarks>
    static string? Rejects(string pattern, int options) {
        try {
            _ = new Regex(pattern, (RegexOptions)(options & ~Compiled));
            return null;
        } catch (ArgumentOutOfRangeException) {
            return null;
        } catch (ArgumentNullException) {
            return null;
        } catch (ArgumentException exception) {
            return Message(exception.Message);
        }
    }

    /// <summary>The first line of the framework's message, which is the part that names the fault.</summary>
    static string Message(string message) {
        var line = message.IndexOf('\n');
        var trimmed = (line < 0 ? message : message.Substring(0, line)).Trim();
        return trimmed.Length == 0 ? "the pattern does not parse" : trimmed;
    }

    /// <summary>
    ///     The expression bound to the parameter of this name, positional or named.
    /// </summary>
    /// <remarks>
    ///     ⚠ Resolved by parameter name rather than by position, because the overload set puts
    ///     <c>pattern</c> first on <c>new Regex(…)</c> and second on <c>Regex.IsMatch(input, pattern)</c>.
    ///     A named argument may also move it anywhere, and a call that omits an optional parameter
    ///     entirely has no expression to read — which is the null this returns.
    /// </remarks>
    static ExpressionSyntax? Argument(ArgumentListSyntax arguments, IMethodSymbol method, string name) {
        var index = -1;
        for (var i = 0; i < method.Parameters.Length; i++) {
            if (string.Equals(method.Parameters[i].Name, name, StringComparison.Ordinal)) {
                index = i;
                break;
            }
        }

        if (index < 0) {
            return null;
        }

        var positional = 0;
        foreach (var argument in arguments.Arguments) {
            if (argument.NameColon is { Name.Identifier.ValueText: { } written }) {
                if (string.Equals(written, name, StringComparison.Ordinal)) {
                    return argument.Expression;
                }

                continue;
            }

            if (positional == index) {
                return argument.Expression;
            }

            positional++;
        }

        return null;
    }
}
