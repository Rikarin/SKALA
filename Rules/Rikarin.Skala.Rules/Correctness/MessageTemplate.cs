using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>One hole in a structured message template, and where its name sits in the raw literal.</summary>
/// <param name="Name">The property name, with any destructuring prefix already stripped.</param>
/// <param name="Start">Offset of <paramref name="Name" /> within the template's <em>value</em>.</param>
/// <param name="Length">Length of <paramref name="Name" /> within the template's value.</param>
readonly record struct TemplateHole(string Name, int Start, int Length);

/// <summary>
///     The message-template grammar Serilog defined and Microsoft.Extensions.Logging adopted.
/// </summary>
/// <remarks>
///     ⚠ <b>The holes are named, not positional, and a name may legitimately repeat.</b> That is the
///     whole difference from <c>string.Format</c>, and it is why <c>CA2241</c> is not the host for any
///     of this: <c>"{X} then {X}"</c> with two arguments is arity-correct, because the arguments are
///     matched to <em>holes</em> in order and not to distinct names. <see cref="TemplateAnalysis" />
///     therefore reports the arity from <see cref="TemplateAnalysis.Holes" /> and the duplication from
///     the names, and the two answers are deliberately independent.
///     <para>
///         ⚠ <c>{{</c> and <c>}}</c> are escapes and are not holes. A parser that misses that reports a
///         template printing a literal brace as having a hole nobody supplied an argument for, which is
///         a false positive on the most ordinary shape there is.
///     </para>
///     <para>
///         ⚠ Serilog's <c>@</c> (destructure) and <c>$</c> (stringify) are part of the <em>hole</em>
///         syntax and not of the name: <c>{@Order}</c> and <c>{Order}</c> are the same property. A
///         parser that keeps the sigil sees two names where the logger sees one and misses the
///         duplicate it exists to find.
///     </para>
/// </remarks>
static class MessageTemplate {
    /// <summary>What one template says, or nothing when it says something this analysis will not judge.</summary>
    internal readonly record struct TemplateAnalysis(IReadOnlyList<TemplateHole> Holes, bool Positional);

    /// <summary>
    ///     Parses a template's text into its holes.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>An all-numeric hole makes the whole template positional and withdraws the analysis.</b>
    ///     Serilog reads <c>"{0} {1}"</c> as indices rather than names, so arity is max-index-plus-one
    ///     rather than a count, and a template mixing the two has semantics no rule should guess at.
    ///     <c>CA2253</c> is the rule that says not to write them; this one declines to judge them.
    /// </remarks>
    internal static TemplateAnalysis Parse(string text) {
        var holes = new List<TemplateHole>();
        var positional = false;

        for (var i = 0; i < text.Length; i++) {
            if (text[i] == '}') {
                // `}}` is an escaped brace; a lone `}` is malformed and the logger renders it verbatim.
                if (i + 1 < text.Length && text[i + 1] == '}') {
                    i++;
                }

                continue;
            }

            if (text[i] != '{') {
                continue;
            }

            if (i + 1 < text.Length && text[i + 1] == '{') {
                i++;
                continue;
            }

            var close = text.IndexOf('}', i + 1);
            if (close < 0) {
                // An unterminated hole is not a hole. Nothing after it can be parsed either.
                break;
            }

            var start = i + 1;
            if (start < close && (text[start] == '@' || text[start] == '$')) {
                start++;
            }

            // The name ends at the alignment (`,`) or the format specifier (`:`), whichever comes first.
            var end = start;
            while (end < close && text[end] != ',' && text[end] != ':') {
                end++;
            }

            var name = text.Substring(start, end - start);
            i = close;

            if (name.Length == 0) {
                continue;
            }

            if (IsAllDigits(name)) {
                positional = true;
                continue;
            }

            holes.Add(new TemplateHole(name, start, name.Length));
        }

        return new TemplateAnalysis(holes, positional);
    }

    static bool IsAllDigits(string name) {
        foreach (var c in name) {
            if (c is < '0' or > '9') {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     The logging APIs whose template parameter this analysis understands, by containing type.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Binding is by containing type and parameter name, never by method name.</b> A rule that
    ///     matched <c>Information</c> or <c>LogError</c> by spelling would fire on every domain method
    ///     that happens to be called that, and would miss the one call written through an alias.
    /// </remarks>
    internal static readonly string[] SerilogTypes = [
        "Serilog.ILogger", "Serilog.Log"
    ];

    internal const string ExtensionsLoggingType = "Microsoft.Extensions.Logging.LoggerExtensions";

    /// <summary>Serilog's logging entry points, or an empty array where Serilog is not referenced.</summary>
    internal static ImmutableArray<INamedTypeSymbol> ResolveSerilog(Compilation compilation) =>
        Resolve(compilation, SerilogTypes);

    internal static ImmutableArray<INamedTypeSymbol> Resolve(Compilation compilation, string[] metadataNames) {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>(metadataNames.Length);
        foreach (var name in metadataNames) {
            if (compilation.GetTypeByMetadataName(name) is { } type) {
                builder.Add(type);
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    ///     Whether this invocation's method is declared by one of the given logging types.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>ReducedFrom</c> first: <c>logger.LogError(…)</c> is an extension method, and the reduced
    ///     symbol's containing type is the extended interface rather than the static class that declares
    ///     the method.
    /// </remarks>
    internal static bool DeclaredBy(IInvocationOperation operation, ImmutableArray<INamedTypeSymbol> types) {
        var declaring = (operation.TargetMethod.ReducedFrom ?? operation.TargetMethod).ContainingType;
        foreach (var type in types) {
            if (SymbolEqualityComparer.Default.Equals(declaring, type)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Serilog spells its template parameter <c>messageTemplate</c>, MEL spells it <c>message</c>,
    ///     and <c>BeginScope</c> spells it <c>messageFormat</c>.
    /// </summary>
    internal static bool IsTemplateParameter(string? name) =>
        name is "messageTemplate" or "message" or "messageFormat";

    /// <summary>Finds the argument carrying the template, by parameter name.</summary>
    internal static IArgumentOperation? FindTemplate(IInvocationOperation operation) {
        foreach (var argument in operation.Arguments) {
            if (IsTemplateParameter(argument.Parameter?.Name)) {
                return argument;
            }
        }

        return null;
    }

    /// <summary>
    ///     How many values the call supplies for the template's holes.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Counted by parameter ordinal, never by filtering out types that are "not values".</b>
    ///     Every one of these APIs puts everything that is not a value — the extension method's own
    ///     <c>this ILogger</c> receiver, the <c>Exception</c>, the <c>LogLevel</c>, the <c>EventId</c> —
    ///     <em>before</em> the template, so "after the template" is the definition rather than a
    ///     heuristic. ⚠ The receiver is the one that would have been missed: a reduced extension-method
    ///     invocation carries it as <c>Arguments[0]</c> with a real <c>Parameter</c>, so a filter
    ///     written by type name counts <c>logger</c> as a supplied value and reports every correct call
    ///     as one argument too many.
    ///     <para>
    ///         ⚠ <b>The count is withdrawn whenever it cannot be known.</b> A <c>params</c> parameter
    ///         handed an existing array — <c>logger.Information(template, args)</c> — has a length this
    ///         analysis cannot see. Roslyn marks the array it synthesises for a genuine <c>params</c>
    ///         expansion as implicit, so the two are distinguishable and only the implicit one is
    ///         counted.
    ///     </para>
    /// </remarks>
    internal static bool TryReadValues(IInvocationOperation operation, IArgumentOperation template, out int count) {
        count = 0;
        var after = template.Parameter!.Ordinal;

        foreach (var argument in operation.Arguments) {
            var parameter = argument.Parameter;
            if (parameter is null || parameter.Ordinal <= after) {
                continue;
            }

            if (!parameter.IsParams) {
                count++;
                continue;
            }

            if (argument.Value is not IArrayCreationOperation { IsImplicit: true } array
                || array.Initializer is null) {
                return false;
            }

            count += array.Initializer.ElementValues.Length;
        }

        return true;
    }
}
