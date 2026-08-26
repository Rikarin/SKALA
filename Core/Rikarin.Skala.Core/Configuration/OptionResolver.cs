using System.Collections.Immutable;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Core.Configuration;

/// <summary>Where a value came from, and under which of the option's spellings.</summary>
public sealed record OptionOrigin(EditorConfigAssignment Assignment, int Specificity) {
    public string File => Assignment.File;
    public int Line => Assignment.Line;
    public string Spelling => Assignment.Key;
    public string Value => Assignment.Value;
}

/// <summary>One option's effective value for one file.</summary>
public sealed record ResolvedOption(
    OptionId Id,
    string Value,
    OptionOrigin? Origin,
    ImmutableArray<OptionOrigin> Candidates) {
    public OptionInfo Info => OptionRegistry.Get(Id);

    /// <summary>True when nothing in the chain set the option and the registry default is in force.</summary>
    public bool IsDefault => Origin is null;

    public string SourceText => Origin is null
        ? "(default)"
        : $"{Origin.File}:{Origin.Line.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
}

/// <summary>A key in the configuration that the registry does not know.</summary>
public sealed record UnknownKey(EditorConfigAssignment Assignment, KeyNamespace Namespace);

/// <summary>
/// What kind of key an unrecognised name is. Only <see cref="Option"/> is an SK9001: the export
/// carries three thousand inspection severities, and a tool that warns about all of them on first
/// run gets uninstalled on first run.
/// </summary>
public enum KeyNamespace {
    /// <summary>A style option Skala does not have in its registry.</summary>
    Option,
    /// <summary><c>resharper_*_highlighting</c> — an inspection severity. Milestone 5.</summary>
    InspectionSeverity,
    /// <summary><c>dotnet_diagnostic.*.severity</c> — a Roslyn analyzer severity. Milestone 5.</summary>
    DiagnosticSeverity,
    /// <summary><c>dotnet_naming_*</c> — Roslyn's own naming engine. Never reimplemented (doc 03).</summary>
    NamingRule,
    /// <summary><c>root</c>, and anything else structural.</summary>
    Structural
}

public sealed record ResolutionResult(
    string SourcePath,
    EditorConfigChain Chain,
    FormattingOptions Options,
    ImmutableArray<ResolvedOption> Resolved,
    ImmutableArray<UnknownKey> Unknown,
    ImmutableArray<string> ValueErrors) {
    public ResolvedOption this[OptionId id] => Resolved[(int)id];

    public IEnumerable<ResolvedOption> Configured => Resolved.Where(static option => !option.IsDefault);
}

/// <summary>
/// Resolves the effective option set for a file, with provenance.
/// </summary>
/// <remarks>
/// docs/plan/03-configuration-model.md § "Precedence": the chain nearest-last, later sections
/// within a file winning over earlier ones, and — the part no other tool implements —
/// <c>resharper_csharp_x</c> beating <c>resharper_x</c> beating <c>csharp_x</c> beating the generic
/// key within one level.
/// </remarks>
public static class OptionResolver {
    static readonly string[] SpecificityPrefixes = ["resharper_csharp_", "resharper_xmldoc_", "resharper_", "csharp_", "xmldoc_", "dotnet_"];

    public static ResolutionResult Resolve(string sourcePath, IReadOnlyList<KeyValuePair<string, string>>? overrides = null) =>
        Resolve(EditorConfigChain.For(sourcePath), overrides);

    public static ResolutionResult Resolve(EditorConfigChain chain, IReadOnlyList<KeyValuePair<string, string>>? overrides = null) {
        var winners = new OptionOrigin?[OptionRegistry.Count];
        var candidates = new List<OptionOrigin>?[OptionRegistry.Count];
        var unknown = ImmutableArray.CreateBuilder<UnknownKey>();
        var errors = ImmutableArray.CreateBuilder<string>();
        var builder = new FormattingOptionsBuilder();

        foreach (var document in chain.Documents) {
            var perDocument = new OptionOrigin?[OptionRegistry.Count];
            foreach (var section in document.Sections) {
                if (!SectionMatcher.Matches(section, chain.SourcePath)) {
                    continue;
                }

                foreach (var assignment in section.Assignments) {
                    if (!OptionRegistry.TryResolve(assignment.Key, out var id)) {
                        unknown.Add(new UnknownKey(assignment, Classify(assignment.Key)));
                        continue;
                    }

                    var origin = new OptionOrigin(assignment, SpecificityOf(assignment.Key));
                    (candidates[(int)id] ??= []).Add(origin);

                    // Later sections win for the same spelling; a more specific spelling wins
                    // outright, whichever section it is in.
                    var previous = perDocument[(int)id];
                    if (previous is null || origin.Specificity <= previous.Specificity) {
                        perDocument[(int)id] = origin;
                    }
                }
            }

            for (var i = 0; i < perDocument.Length; i++) {
                if (perDocument[i] is { } origin) {
                    winners[i] = origin;
                }
            }
        }

        if (overrides is not null) {
            foreach (var (key, value) in overrides) {
                if (!OptionRegistry.TryResolve(key, out var id)) {
                    errors.Add($"--option {key}: not a known option");
                    continue;
                }

                var document = EditorConfigDocument.FromText("(command line)", $"[*]{Environment.NewLine}{key} = {value}{Environment.NewLine}");
                var assignment = document.Sections[1].Assignments[0];
                winners[(int)id] = new OptionOrigin(assignment, -1);
                (candidates[(int)id] ??= []).Add(winners[(int)id]!);
            }
        }

        var resolved = ImmutableArray.CreateBuilder<ResolvedOption>(OptionRegistry.Count);
        for (var i = 0; i < OptionRegistry.Count; i++) {
            var id = (OptionId)i;
            var info = OptionRegistry.Get(id);
            var origin = winners[i];
            if (origin is not null && !builder.TrySet(id, origin.Value, out var error)) {
                errors.Add($"{origin.File}:{origin.Line.ToString(System.Globalization.CultureInfo.InvariantCulture)}: {origin.Spelling} = {origin.Value}: {error}");
                origin = null;
            }

            var value = origin?.Value ?? info.Default ?? string.Empty;
            resolved.Add(new ResolvedOption(id, value, origin, [.. candidates[i] ?? []]));
        }

        return new ResolutionResult(chain.SourcePath, chain, builder.Build(), resolved.MoveToImmutable(), unknown.ToImmutable(), errors.ToImmutable());
    }

    /// <summary>Lower is more specific. docs/plan/03 § "Precedence" step 3.</summary>
    public static int SpecificityOf(string key) {
        for (var i = 0; i < SpecificityPrefixes.Length; i++) {
            if (key.StartsWith(SpecificityPrefixes[i], StringComparison.Ordinal)) {
                return i;
            }
        }

        return SpecificityPrefixes.Length;
    }

    public static KeyNamespace Classify(string key) {
        if (key.EndsWith("_highlighting", StringComparison.Ordinal)) {
            return KeyNamespace.InspectionSeverity;
        }

        if (key.StartsWith("dotnet_diagnostic.", StringComparison.Ordinal)) {
            return KeyNamespace.DiagnosticSeverity;
        }

        if (key.StartsWith("dotnet_naming_", StringComparison.Ordinal)) {
            return KeyNamespace.NamingRule;
        }

        return key is "root" ? KeyNamespace.Structural : KeyNamespace.Option;
    }
}
