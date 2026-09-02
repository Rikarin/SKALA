using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Async;
using Rikarin.Skala.Rules.Cleanup;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Design;
using Rikarin.Skala.Rules.Maintainability;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using Rikarin.Skala.Rules.Performance;
using Rikarin.Skala.Rules.Security;
using Rikarin.Skala.Rules.TestQuality;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         The reference set is the test host's, not a project's, and that is a blind spot rather
    ///         than a detail.
    ///     </b> A real project is compiled against its own reference assemblies, and where
    ///     the two differ a rule can be correct on every fixture and wrong in production with nothing
    ///     failing — overload resolution, <c>params</c> binding and shim visibility all move with the
    ///     reference set. Two measured examples (#297): <c>SK1063</c> declined every
    ///     <c>string.Format</c> call with four or more arguments, because on .NET 9+ the
    ///     <c>params ReadOnlySpan&lt;object?&gt;</c> overload wins and Roslyn reports the argument as
    ///     <c>ParamCollection</c> rather than <c>ParamArray</c>; and <c>SK1060</c> proposed 16 fixes
    ///     that did not compile on <c>netstandard2.0</c>, where <c>System.Index</c> exists but is
    ///     inaccessible. Neither was reachable from here.
    ///     <b>
    ///         The binlog self-sweep, not this harness, is
    ///         the only check that sees a real reference set
    ///     </b>, which is why it is part of shipping a rule.
    ///     <para>
    ///         What the harness <em>can</em> express per fixture is the rest of the compilation:
    ///         <see cref="FixtureCompilation" /> reads <c>// fixture-option:</c> directives for
    ///         <c>LangVersion</c>, <c>DefineConstants</c> and <c>AllowUnsafe</c>, so a rule whose
    ///         territory is below the current language version or inside an <c>#if</c> can be fixtured
    ///         (#317), and <c>unsafe</c> compiles (#310).
    ///     </para>
    ///         A fixture holding top-level statements is compiled as an executable, and until [#314]
    ///         the corpus could not hold one at all.
    ///     </b> Every fixture was a
    ///     <see cref="OutputKind.DynamicallyLinkedLibrary" />, which answers a top-level program with
    ///     <c>CS8805</c> — "Program using top-level statements must be an executable" — and
    ///     <see cref="RuleFixtureTests.Rule_FiresExactlyWhereTheFixtureSaysItShould" /> rejects a fixture
    ///     that does not compile. So the shape a model writes first was one no fixture could describe,
    ///     which is how <c>SK3060</c>'s blindness to it survived: not one rule's oversight but a hole in
    ///     the instrument. The kind is chosen from the file rather than passed in, because a fixture
    ///     with global statements is exactly the one that needs an entry point and a fixture without
    ///     them would draw <c>CS5001</c> from an executable.
    /// </remarks>
    public static CSharpCompilation Compile(
        string source,
        string path,
        LanguageVersion? version = null
    ) {
        var options = FixtureCompilation.From(source);
        var tree = CSharpSyntaxTree.ParseText(
            SourceText.From(source),
            new CSharpParseOptions(version ?? options.LanguageVersion)
                .WithDocumentationMode(DocumentationMode.Parse)
                .WithPreprocessorSymbols(options.PreprocessorSymbols),
            path
        );

        var topLevel = tree.GetRoot() is CompilationUnitSyntax unit
            && unit.Members.Any(static member => member is GlobalStatementSyntax);

        return CSharpCompilation.Create(
            "fixtures",
            [tree],
            References,
            new CSharpCompilationOptions(
                topLevel ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: options.AllowUnsafe,
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
                    } catch (BadImageFormatException) {
                        // ⚠ Deliberate: the trusted-platform list carries native and resource-only
                        // `.dll` files alongside the managed ones, and the only way to tell them apart
                        // is to try. A reference that will not load is one the fixtures do not need.
                    }
                }
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>Every diagnostic Skala's own analyzers produce for one compilation.</summary>
    /// <remarks>
    ///     The settings track <c>AnalyzerHost</c>'s, which is what <c>skala check</c> runs, with one
    ///     deliberate exception (#297):
    ///     <list type="bullet">
    ///         <item>
    ///             ⚠ <c>onAnalyzerException: null</c>, where production installs a handler that records
    ///             <c>SK9030</c> and continues. Null is what turns an analyzer crash into an
    ///             <c>AD0001</c> in the returned diagnostics, and <c>AD0001</c> is the only thing that
    ///             can tell a rule that <em>declined</em> from a rule that <em>threw</em> — production's
    ///             handler swallows it into a SARIF notification no test can see. The difference is
    ///             deliberate and runs in the direction of catching more.
    ///         </item>
    ///         <item>
    ///             <c>concurrentAnalysis: true</c>, as in production. Every Skala analyzer calls
    ///             <c>EnableConcurrentExecution</c>, so running fixtures serially measured a threading
    ///             model no user gets; a rule holding state across callbacks would have been correct on
    ///             every fixture and racy in the field.
    ///         </item>
    ///         <item><c>reportSuppressedDiagnostics: true</c>, as in production.</item>
    ///     </list>
    /// </remarks>
    public static ImmutableArray<Diagnostic> Analyze(
        CSharpCompilation compilation,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        CancellationToken cancellation
    ) =>
        compilation
            .WithAnalyzers(
                analyzers,
                new CompilationWithAnalyzersOptions(
                    new AnalyzerOptions([], new FixtureOptionsProvider()),
                    null,
                    true,
                    false,
                    true
                )
            )
            .GetAnalyzerDiagnosticsAsync(cancellation)
            .GetAwaiter()
            .GetResult();

    /// <summary>Fixture-local EditorConfig values, written as leading // analyzer-option: key = value comments.</summary>
    internal sealed class FixtureOptionsProvider : AnalyzerConfigOptionsProvider {
        public override AnalyzerConfigOptions GlobalOptions => new FixtureOptions(string.Empty);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) =>
            new FixtureOptions(tree.GetText().ToString());

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    }

    sealed class FixtureOptions : AnalyzerConfigOptions {
        readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        public FixtureOptions(string source) {
            foreach (var (key, value) in FixtureCompilation.Directives(source, "// analyzer-option:")) {
                values[key] = value;
            }
        }

        public override IEnumerable<string> Keys => values.Keys;

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value);
    }
}
