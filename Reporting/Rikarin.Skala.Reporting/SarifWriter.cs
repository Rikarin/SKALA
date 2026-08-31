using Microsoft.CodeAnalysis.Sarif;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Globalization;
using System.Reflection;
using System.Text;
using SarifSuppressionKind = Microsoft.CodeAnalysis.Sarif.SuppressionKind;

namespace Rikarin.Skala.Reporting;

/// <summary>
///     The canonical serialisation (ADR-009): one <c>SarifLog</c> per run, everything else rendered
///     from it.
/// </summary>
/// <remarks>
///     ⚠ Built with the SARIF SDK's object model rather than by hand. Writing SARIF by hand is how you
///     produce SARIF that GitHub's code-scanning upload rejects for a reason its error message does not
///     name, and from 1.0 the shape is a compatibility surface (docs/plan/15 § M7).
/// </remarks>
public static class SarifWriter {
    /// <summary>
    ///     ⚠ M5's fingerprint key, kept as a constant because callers name it.
    /// </summary>
    /// <remarks>
    ///     M6 emits <see cref="Fingerprints.Version2" /> beside it; see <see cref="Fingerprints" /> for
    ///     why adding terms is a new version rather than a redefinition.
    /// </remarks>
    public const string FingerprintVersion = Fingerprints.Version1;

    public static SarifLog Build(RunReport report) {
        var driver = new ToolComponent {
            Name = "Skala",
            Version = report.ToolVersion,
            InformationUri = new("https://github.com/Rikarin/Skala"),
            Rules = Rules(report)
        };

        driver.SetProperty("loadMode", report.Mode.ToString().ToLowerInvariant());
        driver.SetProperty("loadSummary", report.LoadSummary);
        driver.SetProperty("configurationFingerprint", report.ConfigurationFingerprint);

        // ⚠ docs/plan/03 § "Precedence": a passing run with `--option` overrides active must not be
        // mistakable for a clean one, so the fact travels in the report rather than in the console.
        driver.SetProperty("optionOverridesActive", report.HasOverrides);

        var run = new Run {
            Tool = new() { Driver = driver, Extensions = Extensions(report) },
            Results = [.. report.Findings.Select(finding => BuildResult(report, finding))],
            Invocations = [BuildInvocation(report)]
        };

        return new() { Runs = [run] };
    }

    /// <summary>
    ///     ⚠ <c>NewLine</c> is set, and must stay set. <see cref="Formatting.Indented" /> breaks lines
    ///     through the <see cref="TextWriter" /> it was handed, whose <c>NewLine</c> defaults to
    ///     <see cref="Environment.NewLine" /> — so <c>--format=json</c> emitted CRLF on Windows, and so
    ///     did every <c>.skala/baseline.sarif</c> written there, which is a committed file that would
    ///     re-diff whole on the first Windows <c>baseline update</c>. Same defect as
    ///     <c>Renderers.Lines</c>, reached through a writer instead of a <c>StringBuilder</c>.
    /// </summary>
    public static string Serialize(SarifLog log) {
        var serializer = JsonSerializer.Create(
            new JsonSerializerSettings {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                ContractResolver = ExplicitLevels
            }
        );

        using var writer = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
        serializer.Serialize(writer, log);
        return writer.ToString();
    }

    /// <summary>
    ///     ⚠ Makes <c>level</c> appear on every result, including the ones Skala means as warnings.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The SARIF SDK declares <c>Result.Level</c> with <c>[DefaultValue(FailureLevel.Warning)]</c>
    ///         and a <c>ShouldSerializeLevel</c>, so a result Skala deliberately set to
    ///         <see cref="FailureLevel.Warning" /> serialises with <b>no <c>level</c> at all</b>. 52 of the
    ///         446 results in Skala's own report were in that state. Nothing downstream was wrong about
    ///         them — SARIF § 3.27.10 and GitHub both default an absent level to <c>warning</c> — but a
    ///         report where the severity Skala chose is present for three of its four values and absent for
    ///         the fourth cannot be read, diffed or grepped, and the absence is indistinguishable from a
    ///         writer that forgot.
    ///     </para>
    ///     <para>
    ///         ⚠ A contract resolver rather than post-processing the JSON. The type-level ⚠ on this class
    ///         says the shape is built with the SDK's object model and never by hand; editing the
    ///         serialiser's contract keeps that true, where a regex over the output would not.
    ///     </para>
    ///     <para>
    ///         ⚠ Static, and it must stay static. A <see cref="DefaultContractResolver" /> caches the
    ///         contract it builds per type, and a fresh instance per call throws that cache away — the
    ///         documented way to make Newtonsoft slow.
    ///     </para>
    /// </remarks>
    static readonly IContractResolver ExplicitLevels = new AlwaysSerializeLevel();

    sealed class AlwaysSerializeLevel : DefaultContractResolver {
        /// <summary>
        ///     The three members the SDK elides at their default and Skala states anyway.
        /// </summary>
        /// <remarks>
        ///     ⚠ <c>ReportingDescriptor.DefaultConfiguration</c> is in the set because the SDK drops the
        ///     whole object, not just the level, when it equals SARIF's default — so forcing
        ///     <c>ReportingConfiguration.Level</c> on its own does nothing, and the 13 rules Skala
        ///     defaults to <c>warning</c> had no <c>defaultConfiguration</c> at all. That is correct
        ///     SARIF, and it is still a rules table you cannot read the mapping off.
        /// </remarks>
        static readonly HashSet<(Type, string)> Forced = [
            (typeof(Result), nameof(Result.Level)),
            (typeof(ReportingConfiguration), nameof(ReportingConfiguration.Level)),
            (typeof(ReportingDescriptor), nameof(ReportingDescriptor.DefaultConfiguration))
        ];

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization serialization) {
            var property = base.CreateProperty(member, serialization);
            if (member.DeclaringType is not { } declaring || !Forced.Contains((declaring, member.Name))) {
                return property;
            }

            property.DefaultValueHandling = DefaultValueHandling.Include;
            property.ShouldSerialize = static _ => true;
            return property;
        }
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
    ///     ⚠ Every rule that <em>could</em> fire, not every rule that did.
    /// </summary>
    /// <remarks>
    ///     doc 09: "A report that does not say which rules could have fired is a report that cannot be
    ///     compared to another." A run with a rule switched off and a run with the rule on and clean
    ///     have the same <c>results</c> array, and they must not look identical.
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
                ShortDescription = new() { Text = rule.Title },
                FullDescription = new() { Text = rule.Rationale },
                Help = new() { Text = rule.Summary },
                HelpUri = new("https://github.com/Rikarin/Skala/blob/main/docs/rules/" + rule.Id + ".md"),

                // ⚠ Level *and* enablement. `RuleSeverity.None` means the rule never runs, which is
                // `enabled: false` in SARIF and not a level at all — see `SarifSeverity`.
                DefaultConfiguration = SarifSeverity.Configuration(rule.DefaultSeverity)
            };

            // ⚠ Skala's own word beside SARIF's level, for the same reason the results carry it: SARIF
            // has no level that distinguishes `hint` from `suggestion` and the catalogue does.
            descriptor.SetProperty("defaultSeverity", rule.DefaultSeverity.ToString().ToLowerInvariant());
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
            Level = SarifSeverity.Level(finding.Severity),
            Message = new() { Text = finding.Message },
            Locations = [
                new Location {
                    PhysicalLocation = new() {
                        ArtifactLocation = new() { Uri = new(uri, UriKind.Relative) },
                        Region = new() {
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

        // ⚠ The exact Skala severity beside the SARIF level, because the mapping is lossy: `hint` and
        // `suggestion` are both `note`. See `SarifSeverity`.
        result.SetProperty(SarifSeverity.Property, SarifSeverity.Word(finding.Severity));

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
                    Description = new() { Text = finding.Message },
                    ArtifactChanges = [
                        .. finding.Fix
                            .GroupBy(static edit => edit.Path, StringComparer.Ordinal)
                            .Select(group => new ArtifactChange {
                                    ArtifactLocation = new() {
                                        Uri = new(Relative(report.RepositoryRoot, group.Key), UriKind.Relative)
                                    },
                                    Replacements = [
                                        .. group.Select(static edit => new Replacement {
                                                DeletedRegion = new() {
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

        if (Suppressions(finding) is { Count: > 0 } suppressions) {
            result.Suppressions = suppressions;
        }

        return result;
    }

    /// <summary>
    ///     The <c>suppressions</c> entries a finding carries — <b>including the baseline's</b>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         ⚠ <b>This is what makes the uploaded report say what the gate decided.</b> Until M9 the
    ///         baseline governed the verdict and was invisible in the SARIF: every accepted finding went
    ///         up to code scanning with no suppression on it, so a page that is supposed to answer "what
    ///         is wrong with master" listed 428 long-accepted findings as open alerts. SARIF § 3.35 has
    ///         the vocabulary for exactly this, and code scanning honours it by showing a suppressed
    ///         result as dismissed rather than open.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>Suppressed, never dropped.</b> Filtering the accepted findings out of the file is a
    ///         different and false claim — "this run did not find them" rather than "this repository has
    ///         accepted them" — and it would take them away from <c>skala report</c>, the PR comment and
    ///         the stored-verdict path, all three of which read this same file (ADR-009).
    ///     </para>
    ///     <para>
    ///         ⚠ Two suppressions on one result is a real state, not a defect: a finding can be both
    ///         <c>#pragma</c>-suppressed in the source and accepted by the baseline, and
    ///         <c>Baseline.Write</c> says why it writes suppressed findings into the baseline in the first
    ///         place. Each entry names its own mechanism in <see cref="SuppressionSourceProperty" /> so
    ///         <see cref="SarifReader" /> can tell them apart on the way back — without that, reading the
    ///         report back turned every baseline entry into a <c>#pragma</c> and dropped it out of
    ///         <see cref="RunReport.Reportable" />, which is the gate's own input.
    ///     </para>
    /// </remarks>
    static List<Suppression> Suppressions(Finding finding) {
        var suppressions = new List<Suppression>();

        if (finding.Suppression != SuppressionKind.None) {
            suppressions.Add(
                Suppress(
                    finding.Suppression == SuppressionKind.Superseded
                        ? SarifSuppressionKind.External
                        : SarifSuppressionKind.InSource,
                    finding.Suppression.ToString(),
                    finding.Suppression.ToString().ToLowerInvariant()
                )
            );
        }

        if (finding.Bucket == BaselineBucket.Existing) {
            suppressions.Add(Suppress(SarifSuppressionKind.External, BaselineJustification, BaselineSuppressionSource));
        }

        return suppressions;
    }

    static Suppression Suppress(SarifSuppressionKind kind, string justification, string source) {
        var suppression = new Suppression {
            Kind = kind,

            // ⚠ Explicit rather than left to the default. SARIF § 3.35.4 does default an absent
            // `status` to `accepted`, but the whole point of this object is to be acted on by a
            // consumer that was not written against Skala, and "the spec says the absence means yes"
            // is a worse thing to rely on than one more field.
            Status = SuppressionStatus.Accepted,
            Justification = justification
        };

        suppression.SetProperty(SuppressionSourceProperty, source);
        return suppression;
    }

    /// <summary>Which mechanism produced a <c>suppressions</c> entry, on the entry itself.</summary>
    /// <remarks>⚠ Read by <see cref="SarifReader" />. Renaming it silently changes what a report means.</remarks>
    public const string SuppressionSourceProperty = "skalaSuppressionSource";

    /// <summary>The <see cref="SuppressionSourceProperty" /> value the baseline writes.</summary>
    public const string BaselineSuppressionSource = "baseline";

    /// <summary>
    ///     The justification on a baseline suppression.
    /// </summary>
    /// <remarks>
    ///     ⚠ It names the file, because the justification is the whole explanation a reader of the
    ///     code-scanning page gets — "external" on its own says a tool outside SARIF dismissed this and
    ///     not which one, and the answer is a reviewed, committed artefact they can open.
    /// </remarks>
    public const string BaselineJustification =
        "Accepted in "
        + Baseline.DefaultRelativePath
        + ", the repository's committed baseline. "
        + "The `ci` gate counts only findings outside it.";

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
                        Level = SarifSeverity.Level(diagnostic.Severity),
                        Message = new() { Text = diagnostic.Message },
                        Descriptor = new() { Id = diagnostic.Id }
                    }
                )
            ];
        }

        return invocation;
    }

    /// <summary>
    ///     docs/plan/09 § "The fingerprint", delegated to <see cref="Fingerprints" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Kept as a member of this type because <c>ReportingTests</c> and every caller outside the
    ///     assembly already name it, and because the SARIF writer is where a reader looks for the
    ///     meaning of <c>partialFingerprints</c>. The computation itself moved: M6 emits
    ///     <see cref="Fingerprints.Version1" /> <em>and</em> <see cref="Fingerprints.Version2" />, and a
    ///     version pair is the whole reason a baseline written before the fingerprint gained the
    ///     enclosing symbol and the ordinal still reads.
    /// </remarks>
    public static string Fingerprint(Finding finding) => Fingerprints.V1(finding);

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
    /// <para>
    /// ⚠ <b>The obvious one-liner here was wrong three ways, and each one printed absolute paths
    /// into an output doc 10 caps at 8 000 characters.</b> It was
    /// <c>path.StartsWith(root, Ordinal)</c>, which:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <b>Compared case-sensitively.</b> On Windows and on a case-insensitive macOS volume the
    /// repository root arrives as <c>C:\Src\Repo</c> and the file as <c>c:\src\repo\a.cs</c>
    /// whenever either came from a different API, and every path in the report fell back to
    /// absolute. This is doc 12 § "Cross-platform"'s case-insensitive-path hazard, reached through
    /// the reporting layer rather than the cache key.
    /// </item>
    /// <item>
    /// <b>Had no component boundary.</b> A root of <c>/src/repo</c> and a sibling
    /// <c>/src/repo-old/a.cs</c> passed the prefix test and rendered as <c>../repo-old/a.cs</c> —
    /// a "repo-relative" path escaping the repository.
    /// </item>
    /// <item>
    /// <b>Took a non-nullable <c>root</c> that callers reach with a nullable one.</b>
    /// <c>RunReport.RepositoryRoot</c> is <c>string?</c>; a null root threw out of a renderer whose
    /// job is to be the thing that never fails.
    /// </item>
    /// </list>
    public static string Relative(string? root, string path) {
        var normalised = path.Replace('\\', '/');
        if (string.IsNullOrEmpty(root) || !Path.IsPathRooted(path)) {
            return normalised;
        }

        // Trailing separators would otherwise make the boundary check below reject a legitimate
        // root, and `Path.GetRelativePath` is indifferent to them.
        var trimmed = root.Replace('\\', '/').TrimEnd('/');
        if (trimmed.Length == 0) {
            return normalised;
        }

        // ⚠ The comparison is ordinal-case-insensitive on the platforms whose file systems are, and
        // ordinal where they are not — matching the same decision the cache key makes, so a path
        // that relativises here is a path that hits the cache there.
        if (!normalised.StartsWith(trimmed, PathComparison)) {
            return normalised;
        }

        // The character after the root must be a separator, or the "root" is a prefix of a sibling
        // directory's name rather than an ancestor.
        if (normalised.Length != trimmed.Length && normalised[trimmed.Length] != '/') {
            return normalised;
        }

        return Path.GetRelativePath(trimmed, normalised).Replace('\\', '/');
    }

    /// <summary>
    ///     ⚠ How two paths are compared for identity, in one place. macOS's default volume and every
    ///     Windows volume are case-insensitive; Linux's are not. Getting this wrong in one direction
    ///     prints absolute paths, and in the other merges two genuinely distinct files on Linux.
    /// </summary>
    public static StringComparison PathComparison { get; } =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
}
