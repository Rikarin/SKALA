using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Sarif;
using Newtonsoft.Json;
using Rikarin.Skala.Core.Diagnostics;

namespace Rikarin.Skala.Reporting;

/// <summary>
///     Reads a <see cref="RunReport" /> back out of a SARIF file.
/// </summary>
/// <remarks>
///     ⚠ This is what makes <c>skala report</c> possible, and doc 09 is explicit about why it must
///     exist: "<c>skala report</c> re-renders a stored SARIF without re-running anything, which is what
///     CI uses to produce a PR comment from an artifact". A CI job that had to re-analyse in order to
///     comment would either re-analyse a different tree or double the build's cost.
///     <para>
///         ⚠ It reads Skala's own SARIF, including the <c>properties</c> the writer put there. A foreign
///         SARIF still loads — that is the value of the format — but the metrics, the gate verdict and the
///         baseline buckets simply will not be in it, and the reader says nothing rather than inventing
///         them.
///     </para>
/// </remarks>
public static class SarifReader {
    public static RunReport Read(string path, string repositoryRoot) {
        var log = JsonConvert.DeserializeObject<SarifLog>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"{path} is not a SARIF log.");

        if (log.Runs is not { Count: > 0 }) {
            throw new InvalidDataException($"{path} has no runs.");
        }

        var run = log.Runs[0];
        var driver = run.Tool?.Driver;
        var invocation = run.Invocations is { Count: > 0 } ? run.Invocations[0] : null;

        var findings = ImmutableArray.CreateBuilder<Finding>();
        foreach (var result in run.Results ?? []) {
            findings.Add(Convert(result, repositoryRoot));
        }

        var gateName = Property<string>(invocation, "gate");

        return new RunReport {
            RepositoryRoot = repositoryRoot,
            Mode = ParseMode(Property<string>(driver, "loadMode")),
            Findings = findings.ToImmutable(),
            LoadSummary = Property<string>(driver, "loadSummary") ?? string.Empty,
            ConfigurationFingerprint = Property<string>(driver, "configurationFingerprint") ?? string.Empty,
            HasOverrides = Property<bool?>(driver, "optionOverridesActive") ?? false,
            FileCount = Property<int?>(invocation, "fileCount") ?? 0,
            LineCount = Property<int?>(invocation, "lineCount") ?? 0,
            Partial = Property<bool?>(invocation, "partial") ?? false,
            ToolVersion = driver?.Version ?? SkalaVersion.Value,
            Duration = invocation is { StartTimeUtc: { } start, EndTimeUtc: { } end } && end > start
                ? end - start
                : TimeSpan.Zero,
            Gate = gateName is { Length: > 0 }
                ? new GateResult(
                    gateName,
                    Property<bool?>(invocation, "gatePassed") ?? true,
                    [.. Property<string[]>(invocation, "gateFailures") ?? []]
                )
                : null
        };
    }

    static Finding Convert(Result result, string repositoryRoot) {
        var location = result.Locations is { Count: > 0 } ? result.Locations[0].PhysicalLocation : null;
        var region = location?.Region;
        var relative = location?.ArtifactLocation?.Uri?.ToString() ?? string.Empty;

        return new Finding {
            RuleId = result.RuleId ?? string.Empty,
            Severity = Severity(result.Level),
            Message = result.Message?.Text ?? string.Empty,
            Path = relative.Length == 0 ? repositoryRoot : Path.Combine(repositoryRoot, relative),
            Line = region?.StartLine ?? 0,
            Column = region?.StartColumn ?? 0,
            EndLine = region?.EndLine ?? 0,
            EndColumn = region?.EndColumn ?? 0,
            Start = region?.CharOffset ?? 0,
            Length = region?.CharLength ?? 0,
            EnclosingSymbol = Property<string>(result, "enclosingSymbol") ?? string.Empty,
            OrdinalWithinSymbol = Property<int?>(result, "ordinalWithinSymbol") ?? 0,
            IsInChangedCode = Property<bool?>(result, "inChangedCode") ?? false,
            Bucket = Property<string>(result, "baseline") switch {
                "new" => BaselineBucket.New,
                "existing" => BaselineBucket.Existing,
                _ => BaselineBucket.Unknown
            },

            // ⚠ The fix edits are not reconstructed. `skala report` renders; it never applies, and a
            // half-reconstructed fix in a report is a fix somebody will try to use.
            Fix = [],
            FixIsSafe = Property<bool?>(result, "fixIsSafe") ?? false,
            Suppression = result.Suppressions is { Count: > 0 } ? SuppressionKind.Pragma : SuppressionKind.None
        };
    }

    static LoadMode ParseMode(string? value) =>
        value switch {
            "binlog" => LoadMode.Binlog,
            "workspace" => LoadMode.Workspace,
            _ => LoadMode.Loose
        };

    static SkalaSeverity Severity(FailureLevel level) =>
        level switch {
            FailureLevel.Error => SkalaSeverity.Error,
            FailureLevel.Warning => SkalaSeverity.Warning,
            FailureLevel.Note => SkalaSeverity.Info,
            _ => SkalaSeverity.Hidden
        };

    static T? Property<T>(PropertyBagHolder? holder, string name) {
        if (holder?.PropertyNames is null || !holder.PropertyNames.Contains(name)) {
            return default;
        }

        try {
            return holder.GetProperty<T>(name);
        } catch (Exception exception) when (exception is JsonException or InvalidOperationException) {
            return default;
        }
    }
}
