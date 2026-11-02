using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Analysis.Hosting;

/// <summary>
///     The severity a hosted Roslyn id reports at when the project configured nothing for it — the
///     lowest-precedence answer there is.
/// </summary>
/// <remarks>
///     ⚠ <b>Asked last, and only for an id the project is silent about.</b> Each of these wins over the
///     default, because each is a project saying something about the id:
///     <list type="bullet">
///         <item>
///             <c>dotnet_diagnostic.&lt;id&gt;.severity</c> in any <c>.editorconfig</c> section or global
///             config — read by <see cref="EditorConfigOptions" /> before this is asked;
///         </item>
///         <item>
///             the compilation's own <c>SpecificDiagnosticOptions</c> — <c>NoWarn</c>,
///             <c>WarningsAsErrors</c>, a rule set — which the build gave the compiler;
///         </item>
///         <item>
///             a <c>:severity</c> suffix on the id's style option
///             (<c>csharp_style_prefer_primary_constructors = true:none</c>), honoured as written;
///         </item>
///         <item>
///             a bulk <c>dotnet_analyzer_diagnostic.category-&lt;Category&gt;.severity</c> or
///             <c>dotnet_analyzer_diagnostic.severity</c>, which Roslyn applies only to an id that has
///             no specific value — so answering here would silently outrank it.
///         </item>
///     </list>
///     ⚠ This is a provider answer rather than a synthetic <c>is_global</c> config at a low
///     <c>global_level</c>, which was the first design. A global config ranks below every
///     <c>.editorconfig</c> section, but its value is still a <em>specific</em> severity, and the last
///     two items above would lose to it.
///     <para>
///         ⚠ The ids the selected analyzers carry and Skala does not host answer <c>none</c>, always:
///         Roslyn skips an analyzer's work for a suppressed id, and <see cref="AnalyzerHost" /> drops
///         any that a project switched on anyway.
///     </para>
/// </remarks>
public sealed class HostedSeverityDefaults(ImmutableDictionary<string, ReportDiagnostic> commandLine) {
    /// <summary>What goes into every analysis cache key, so a change to a default moves the key.</summary>
    public static string Fingerprint { get; } =
        "hosted-defaults:"
        + string.Join(
            ",",
            RoslynCodeStyle.DefaultSeverities.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => pair.Key + "=" + pair.Value.ToString())
        )
        + ";";

    public bool TryGet(
        string id,
        IReadOnlyDictionary<string, string>? treeOptions,
        IReadOnlyDictionary<string, string>? globalOptions,
        out ReportDiagnostic severity
    ) {
        severity = ReportDiagnostic.Default;
        var codeStyle = RoslynCodeStyle.Load();
        if (codeStyle.SilencedIds.Contains(id)) {
            severity = ReportDiagnostic.Suppress;
            return true;
        }

        if (!RoslynCodeStyle.DefaultSeverities.TryGetValue(id, out var fallback) || commandLine.ContainsKey(id)) {
            return false;
        }

        if (RoslynCodeStyle.StyleOptions.TryGetValue(id, out var option)
            && (Find(treeOptions, option) ?? Find(globalOptions, option)) is { } value) {
            var colon = value.LastIndexOf(':');
            if (colon >= 0) {
                if (Parse(value[(colon + 1)..].Trim()) is { } suffixed) {
                    severity = suffixed;
                    return true;
                }

                // An unreadable suffix is the project's configuration being wrong, not silent; the
                // analyzer reads it the way Roslyn does and Skala adds nothing on top.
                return false;
            }
        }

        var category = codeStyle.Categories.GetValueOrDefault(id);
        if (Find(treeOptions, "dotnet_analyzer_diagnostic.severity") is not null
            || Find(globalOptions, "dotnet_analyzer_diagnostic.severity") is not null
            || (category is not null
                && (Find(treeOptions, $"dotnet_analyzer_diagnostic.category-{category}.severity") is not null
                    || Find(globalOptions, $"dotnet_analyzer_diagnostic.category-{category}.severity") is not null))) {
            return false;
        }

        severity = fallback;
        return true;
    }

    /// <summary>Analyzer-config keys are case-insensitive; Roslyn lowercases them, a hand-built map may not.</summary>
    static string? Find(IReadOnlyDictionary<string, string>? options, string key) {
        if (options is null) {
            return null;
        }

        if (options.TryGetValue(key, out var exact) || options.TryGetValue(key.ToLowerInvariant(), out exact)) {
            return exact;
        }

        foreach (var (candidate, value) in options) {
            if (string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase)) {
                return value;
            }
        }

        return null;
    }

    static ReportDiagnostic? Parse(string text) =>
        text.ToLower(CultureInfo.InvariantCulture) switch {
            "none" => ReportDiagnostic.Suppress,
            "silent" or "refactoring" => ReportDiagnostic.Hidden,
            "suggestion" => ReportDiagnostic.Info,
            "warning" => ReportDiagnostic.Warn,
            "error" => ReportDiagnostic.Error,
            // `default` included: it names no severity of its own, so the analyzer decides.
            _ => null
        };
}
