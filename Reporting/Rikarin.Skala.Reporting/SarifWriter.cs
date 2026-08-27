using System.Globalization;
using System.IO.Hashing;
using System.Text;
using Microsoft.CodeAnalysis.Sarif;
using Newtonsoft.Json;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using SarifSuppressionKind = Microsoft.CodeAnalysis.Sarif.SuppressionKind;

namespace Rikarin.Skala.Reporting;

/// <summary>
/// The canonical serialisation (ADR-009): one <c>SarifLog</c> per run, everything else rendered
/// from it.
/// </summary>
/// <remarks>
/// ⚠ Built with the SARIF SDK's object model rather than by hand. Writing SARIF by hand is how you
/// produce SARIF that GitHub's code-scanning upload rejects for a reason its error message does not
/// name, and from 1.0 the shape is a compatibility surface (docs/plan/15 § M7).
/// </remarks>
public static class SarifWriter {
    /// <summary>
    /// ⚠ M5's fingerprint key, kept as a constant because callers name it.
    /// </summary>
    /// <remarks>
    /// M6 emits <see cref="Fingerprints.Version2"/> beside it; see <see cref="Fingerprints"/> for
    /// why adding terms is a new version rather than a redefinition.
    /// </remarks>
    public const string FingerprintVersion = Fingerprints.Version1;

    public static SarifLog Build(RunReport report) {
        var driver = new ToolComponent {
            Name = "Skala",
            Version = report.ToolVersion,
            InformationUri = new Uri("https://github.com/Rikarin/Skala"),
            Rules = Rules(report)
        };

        driver.SetProperty("loadMode", report.Mode.ToString().ToLowerInvariant());
        driver.SetProperty("loadSummary", report.LoadSummary);
        driver.SetProperty("configurationFingerprint", report.ConfigurationFingerprint);

        // ⚠ docs/plan/03 § "Precedence": a passing run with `--option` overrides active must not be
        // mistakable for a clean one, so the fact travels in the report rather than in the console.
        driver.SetProperty("optionOverridesActive", report.HasOverrides);

        var run = new Run {
            Tool = new Tool { Driver = driver, Extensions = Extensions(report) },
            Results = [.. report.Findings.Select(finding => BuildResult(report, finding))],
            Invocations = [BuildInvocation(report)]
        };

        return new SarifLog { Runs = [run] };
    }

    public static string Serialize(SarifLog log) {
        var serializer = JsonSerializer.Create(
            new JsonSerializerSettings {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                DateFormatHandling = DateFormatHandling.IsoDateFormat
            }
        );

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        serializer.Serialize(writer, log);
        return writer.ToString();
    }

    static List<ToolComponent>? Extensions(RunReport report) {
        if (report.Extensions.IsEmpty) {
            return null;
        }

        var result = new List<ToolComponent>();
        foreach (var extension in report.Extensions) {
            var component = new ToolComponent { Name = extension.Name, Version = extension.Version };
            component.SetProperty("ruleCount", extension.RuleCount);
            result.Add(component);
        }

        return result;
    }

    /// <summary>
    /// ⚠ Every rule that <em>could</em> fire, not every rule that did.
    /// </summary>
    /// <remarks>
    /// doc 09: "A report that does not say which rules could have fired is a report that cannot be
    /// compared to another." A run with a rule switched off and a run with the rule on and clean
    /// have the same <c>results</c> array, and they must not look identical.
    /// </remarks>
    static List<ReportingDescriptor> Rules(RunReport report) {
        var skipped = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rule in report.SkippedRules) {
            skipped[rule.RuleId] = rule.Reason;
        }

        var result = new List<ReportingDescriptor>();
        foreach (var rule in RuleCatalog.All) {
            var descriptor = new ReportingDescriptor {
                Id = rule.Id,
                Name = Pascal(rule.Concept),
                ShortDescription = new MultiformatMessageString { Text = rule.Title },
                FullDescription = new MultiformatMessageString { Text = rule.Rationale },
                Help = new MultiformatMessageString { Text = rule.Summary },
                HelpUri = new Uri("https://github.com/Rikarin/Skala/blob/main/docs/rules/" + rule.Id + ".md"),
                DefaultConfiguration = new ReportingConfiguration { Level = Level(rule.DefaultSeverity) }
            };

            descriptor.SetProperty("category", rule.Category);
            descriptor.SetProperty("scope", rule.Scope.ToString());
            descriptor.SetProperty("requiresSemantics", rule.RequiresSemantics);
            descriptor.SetProperty("hasFix", rule.HasFix);
            descriptor.SetProperty("fixIsSafe", rule.FixIsSafe);
            descriptor.SetProperty("since", rule.Since);

            if (rule.LanguageVersion is { } floor) {
                descriptor.SetProperty("languageVersion", floor);
            }

            if (rule.ReSharperSeverityKey is { } key) {
                descriptor.SetProperty("resharperSeverityKey", key);
            }

            if (skipped.TryGetValue(rule.Id, out var reason)) {
                descriptor.SetProperty("skipped", reason);
            }

            result.Add(descriptor);
        }

        return result;
    }

    static Result BuildResult(RunReport report, Finding finding) {
        var uri = Relative(report.RepositoryRoot, finding.Path);
        var result = new Result {
            RuleId = finding.RuleId,
            Level = Level(finding.Severity),
            Message = new Message { Text = finding.Message },
            Locations = [
                new Location {
                    PhysicalLocation = new PhysicalLocation {
                        ArtifactLocation = new ArtifactLocation { Uri = new Uri(uri, UriKind.Relative) },
                        Region = new Region {
                            StartLine = Math.Max(1, finding.Line),
                            StartColumn = Math.Max(1, finding.Column),
                            EndLine = Math.Max(1, finding.EndLine == 0 ? finding.Line : finding.EndLine),
                            EndColumn = Math.Max(1, finding.EndColumn == 0 ? finding.Column : finding.EndColumn),
                            CharOffset = finding.Start,
                            CharLength = finding.Length
                        }
                    }
                }
            ],
            PartialFingerprints = Fingerprints.For(finding)
        };

        if (!finding.TargetFrameworks.IsEmpty) {
            result.SetProperty("tfms", finding.TargetFrameworks.ToArray());
        }

        // ⚠ The fingerprint's own inputs travel beside the hash. A baseline diff that shows only
        // opaque hashes is unreviewable, and doc 09 makes reviewing it the point of the file.
        if (finding.EnclosingSymbol.Length > 0) {
            result.SetProperty("enclosingSymbol", finding.EnclosingSymbol);
        }

        if (finding.OrdinalWithinSymbol > 0) {
            result.SetProperty("ordinalWithinSymbol", finding.OrdinalWithinSymbol);
        }

        if (finding.Bucket != BaselineBucket.Unknown) {
            result.SetProperty("baseline", finding.Bucket.ToString().ToLowerInvariant());
        }

        if (report.ChangedCodeReference is not null) {
            result.SetProperty("inChangedCode", finding.IsInChangedCode);
        }

        if (finding.HasFix) {
            result.Fixes = [
                new Fix {
                    Description = new Message { Text = finding.Message },
                    ArtifactChanges = [
                        .. finding.Fix
                            .GroupBy(static edit => edit.Path, StringComparer.Ordinal)
                            .Select(group => new ArtifactChange {
                                    ArtifactLocation = new ArtifactLocation {
                                        Uri = new Uri(Relative(report.RepositoryRoot, group.Key), UriKind.Relative)
                                    },
                                    Replacements = [
                                        .. group.Select(static edit => new Replacement {
                                                DeletedRegion = new Region {
                                                    CharOffset = edit.Start, CharLength = edit.Length
                                                },
                                                InsertedContent = edit.Text.Length == 0
                                                    ? null
                                                    : new ArtifactContent { Text = edit.Text }
                                            }
                                        )
                                    ]
                                }
                            )
                    ]
                }
            ];

            result.SetProperty("fixIsSafe", finding.FixIsSafe);
        }

        if (finding.Suppression != SuppressionKind.None) {
            result.Suppressions = [
                new Suppression {
                    Kind = finding.Suppression == SuppressionKind.Superseded
                        ? SarifSuppressionKind.External
                        : SarifSuppressionKind.InSource,
                    Justification = finding.Suppression.ToString()
                }
            ];
        }

        return result;
    }

    static Invocation BuildInvocation(RunReport report) {
        var end = DateTime.UtcNow;
        var invocation = new Invocation {
            ExecutionSuccessful = !report.Partial,
            ExitCode = report.Gate is { Passed: false } ? ExitCodes.GateFailed : ExitCodes.Ok,
            StartTimeUtc = end - report.Duration,
            EndTimeUtc = end
        };

        invocation.SetProperty("partial", report.Partial);
        invocation.SetProperty("fileCount", report.FileCount);
        invocation.SetProperty("lineCount", report.LineCount);
        invocation.SetProperty(
            "skippedRules",
            report.SkippedRules.Select(static rule => rule.RuleId + ": " + rule.Reason).ToArray()
        );

        if (report.Gate is { } gate) {
            invocation.SetProperty("gate", gate.Name);
            invocation.SetProperty("gatePassed", gate.Passed);
            invocation.SetProperty("gateFailures", gate.Failures.ToArray());
        }

        if (!report.Diagnostics.IsEmpty) {
            invocation.ToolExecutionNotifications = [
                .. report.Diagnostics.Select(static diagnostic =>
                        new Notification {
                            Level = Level(diagnostic.Severity),
                            Message = new Message { Text = diagnostic.Message },
                            Descriptor = new ReportingDescriptorReference { Id = diagnostic.Id }
                        }
                )
            ];
        }

        return invocation;
    }

    /// <summary>
    /// docs/plan/09 § "The fingerprint", delegated to <see cref="Fingerprints"/>.
    /// </summary>
    /// <remarks>
    /// ⚠ Kept as a member of this type because <c>ReportingTests</c> and every caller outside the
    /// assembly already name it, and because the SARIF writer is where a reader looks for the
    /// meaning of <c>partialFingerprints</c>. The computation itself moved: M6 emits
    /// <see cref="Fingerprints.Version1"/> <em>and</em> <see cref="Fingerprints.Version2"/>, and a
    /// version pair is the whole reason a baseline written before the fingerprint gained the
    /// enclosing symbol and the ordinal still reads.
    /// </remarks>
    public static string Fingerprint(Finding finding) => Fingerprints.V1(finding);

    static FailureLevel Level(SkalaSeverity severity) =>
        severity switch {
            SkalaSeverity.Error => FailureLevel.Error,
            SkalaSeverity.Warning => FailureLevel.Warning,
            SkalaSeverity.Info => FailureLevel.Note,
            _ => FailureLevel.None
        };

    static FailureLevel Level(RuleSeverity severity) =>
        severity switch {
            RuleSeverity.Error => FailureLevel.Error,
            RuleSeverity.Warning => FailureLevel.Warning,
            RuleSeverity.Suggestion => FailureLevel.Note,
            _ => FailureLevel.None
        };

    static string Pascal(string concept) {
        var builder = new StringBuilder(concept.Length);
        var upper = true;
        foreach (var c in concept) {
            if (c is '-' or '_') {
                upper = true;
                continue;
            }

            builder.Append(upper ? char.ToUpperInvariant(c) : c);
            upper = false;
        }

        return builder.ToString();
    }

    /// <summary>
    /// A repository-relative, forward-slashed path — how every surface displays a file.
    /// </summary>
    /// <remarks>
    /// ⚠ Public because the commands outside this assembly display paths too, and a second
    /// implementation would eventually disagree about the separator on Windows.
    /// </remarks>
    public static string Relative(string root, string path) =>
        Path.IsPathRooted(path) && path.StartsWith(root, StringComparison.Ordinal)
            ? Path.GetRelativePath(root, path).Replace('\\', '/')
            : path.Replace('\\', '/');
}
