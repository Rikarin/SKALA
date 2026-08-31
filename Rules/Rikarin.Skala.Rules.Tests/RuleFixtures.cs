using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Reflection;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>One fixture file: which rule it is about, and whether the rule should fire on it.</summary>
public sealed record RuleFixture(string RuleId, bool ShouldFire, string Path) {
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

    public override string ToString() => RuleId + (ShouldFire ? "/+" : "/−") + "/" + Name;
}

/// <summary>
///     The rule unit level: run one analyzer over one file and see what it says.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/16 § R3's shipping bar is not "the rule works". It is
///     <b>
///         zero false positives on the
///         reference corpus, a documented false-positive story, and a "should not fire" fixture set at
///         least as large as the positive one
///     </b> — because the rules most likely to over-fire are exactly
///     the ones with the most value, and a rule that fires 400 times and is right 390 is not ready.
///     <see cref="RuleFixtureTests.EveryRule_HasMoreNegativeFixturesThanPositive" /> is that bar as a
///     test.
/// </remarks>
public static class RuleFixtures {
    public static string Root { get; } = Path.Combine(
        Assembly.GetExecutingAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(static attribute => attribute.Key == "SkalaRepositoryRoot")
        .Value!,
        "Rules",
        "Rikarin.Skala.Rules.Tests",
        "fixtures"
    );

    public static IReadOnlyList<RuleFixture> All() {
        if (!Directory.Exists(Root)) {
            return [];
        }

        var result = new List<RuleFixture>();
        foreach (var directory in Directory.GetDirectories(Root).OrderBy(static d => d, StringComparer.Ordinal)) {
            var ruleId = Path.GetFileName(directory);
            foreach (var (folder, shouldFire) in new[] { ("positive", true), ("negative", false) }) {
                var path = Path.Combine(directory, folder);
                if (!Directory.Exists(path)) {
                    continue;
                }

                foreach (var file in Directory.GetFiles(path, "*.cs").OrderBy(static f => f, StringComparer.Ordinal)) {
                    result.Add(new RuleFixture(ruleId, shouldFire, file));
                }
            }
        }

        return result;
    }

    /// <summary>
    ///     A compilation over the running framework's reference set, which is what loose mode gives a
    ///     rule and therefore the least the rule may assume.
    /// </summary>
    public static CSharpCompilation Compile(
        string source,
        string path,
        LanguageVersion version = LanguageVersion.Preview
    ) {
        var tree = CSharpSyntaxTree.ParseText(
            SourceText.From(source),
            new CSharpParseOptions(version).WithDocumentationMode(DocumentationMode.Parse),
            path
        );

        return CSharpCompilation.Create(
            "fixtures",
            [tree],
            References,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                specificDiagnosticOptions: OptIn
            )
        );
    }

    /// <summary>
    ///     ⚠ The rules that ship <c>defaultSeverity: none</c>, turned on for the fixture harness.
    /// </summary>
    /// <remarks>
    ///     A rule that is disabled by default is one Roslyn's severity filter drops before the analyzer's
    ///     diagnostic reaches anybody — so without this, its positive fixtures would prove that the
    ///     filter works and nothing at all about the rule. Turning it on here is the same thing a
    ///     repository does with <c>dotnet_diagnostic.SK7010.severity</c> per path, which is how
    ///     rules.json says the rule is meant to be used.
    /// </remarks>
    static ImmutableDictionary<string, ReportDiagnostic> OptIn { get; } = BuildOptIn();

    static ImmutableDictionary<string, ReportDiagnostic> BuildOptIn() {
        var builder = ImmutableDictionary.CreateBuilder<string, ReportDiagnostic>(StringComparer.Ordinal);
        foreach (var rule in RuleCatalog.All) {
            if (!rule.Retired && rule.DefaultSeverity == RuleSeverity.None) {
                builder[rule.Id] = ReportDiagnostic.Warn;
            }
        }

        return builder.ToImmutable();
    }

    public static ImmutableArray<MetadataReference> References { get; } = Build();

    static ImmutableArray<MetadataReference> Build() {
        var builder = ImmutableArray.CreateBuilder<MetadataReference>();
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string assemblies) {
            foreach (var path in assemblies.Split(Path.PathSeparator)) {
                if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
                    try {
                        builder.Add(MetadataReference.CreateFromFile(path));
                    } catch (BadImageFormatException) { }
                }
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>Every diagnostic Skala's own analyzers produce for one compilation.</summary>
    public static ImmutableArray<Diagnostic> Analyze(
        CSharpCompilation compilation,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        CancellationToken cancellation
    ) =>
        compilation
            .WithAnalyzers(
                analyzers,
                new CompilationWithAnalyzersOptions(
                    new AnalyzerOptions([]),
                    null,
                    false,
                    false,
                    true
                )
            )
            .GetAnalyzerDiagnosticsAsync(cancellation)
            .GetAwaiter()
            .GetResult();
}
