using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Core.Tests;

public sealed class EditorConfigIngestionTests {
    [Fact]
    public void Template_ParsesIntoTheThreeSectionsThePlanCounted() {
        var document = EditorConfigDocument.Load(RepositoryPaths.Template);
        var named = document.Sections.Where(static section => section.Name is not null).ToArray();

        Assert.Equal(3, named.Length);
        Assert.Equal("*", named[0].Name);
        Assert.Equal("*.csv", named[1].Name);
        Assert.Contains("cs,", named[2].Name, StringComparison.Ordinal);
        // ⚠ Measured from the file rather than pinned to a literal. The template is an *input*: the
        // author stripped the C++, VB and F# namespaces from it (4 238 lines to 2 178, 1 896
        // resharper_cpp_* keys among them) and a hard-coded 4 226 turned a deliberate edit into four
        // red tests. What is worth asserting is that ingestion sees every assignment the file has.
        var expected = File.ReadAllLines(RepositoryPaths.Template)
            .Count(static line => {
                    var trimmed = line.Trim();
                    return trimmed.Length > 0
                        && !trimmed.StartsWith('#')
                        && !trimmed.StartsWith('[')
                        && trimmed.Contains('=', StringComparison.Ordinal);
                }
            );

        Assert.Equal(expected, document.Assignments.Count());
        Assert.False(document.IsRoot);
    }

    /// <summary>
    ///     ADR-015 — Skala formats Skala, and the configuration it formats itself with is the export.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>This used to assert byte equality: <c>own == "root = true\n\n" + template</c>.</b> That
    ///     spelling was doing two jobs at once, and only one of them was this one. The other — "the
    ///     bytes <c>jb cleanupcode</c> is handed are the export" — was load-bearing for the whole
    ///     conformance corpus and was being asserted here only by accident, because the oracle harness
    ///     happened to copy this same file. It now has its own home and its own test:
    ///     <c>Rikarin.Skala.Testing.OracleEditorConfig</c> and
    ///     <c>ProvenanceTests.TheOracleIsConfiguredByTheExport</c>.
    ///     <para>
    ///         What is left here is the claim this test was written to make, restated over resolved
    ///         options rather than over bytes: whatever Skala reads when it formats its own source
    ///         configures Skala exactly as the export does. That is the defect the byte comparison
    ///         actually caught — a hand-edit to <c>.editorconfig</c>, or a re-export that was not
    ///         propagated, leaving Skala formatting itself under a configuration nobody exported — and
    ///         stating it over <see cref="OptionId" /> and value rather than over text keeps it true
    ///         when the two files stop agreeing on how a key is <em>spelled</em>.
    ///     </para>
    ///     <para>
    ///         ⚠ <c>root = true</c> is asserted separately and is not cosmetic: it is the premise of
    ///         <see cref="ChainWalk_StopsAtRoot" />, and without it Skala's own chain walk climbs out of
    ///         the repository into whatever a checkout happens to sit under.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>The export side now goes through <see cref="CanonicalEditorConfig.Translate" />.</b>
    ///         It has to: the export is spelled in ReSharper's key namespace and Skala no longer reads
    ///         a single key of it, so resolving the raw file would resolve the two dozen standard
    ///         EditorConfig keys and nothing else — and the comparison would be between 25 options and
    ///         436. Stating it over the translation is the same claim, not a weaker one: what is
    ///         asserted is still that every option Skala configures itself with holds the value the
    ///         export sets for it, and it now also fails if <c>Translate</c> drops or mis-maps a key,
    ///         which is the step the canonical payload for eighteen repositories is built from.
    ///     </para>
    /// </remarks>
    [Fact]
    public void RepositoryEditorConfig_DeclaresRootAndConfiguresSkalaExactlyAsTheExportDoes() {
        Assert.StartsWith("root = true", File.ReadAllText(RepositoryPaths.EditorConfig), StringComparison.Ordinal);
        Assert.True(EditorConfigDocument.Load(RepositoryPaths.EditorConfig).IsRoot);

        var own = ConfiguredOptions(EditorConfigChain.For(RepositoryPaths.SampleSourceFile));
        var export = ConfiguredOptions(
            EditorConfigChain.Of(
                RepositoryPaths.SampleSourceFile,
                // ⚠ The template's own path, not a made-up name: a section glob is matched relative
                // to the directory the .editorconfig sits in, so a synthetic path matches nothing and
                // the export side comes back empty.
                EditorConfigDocument.FromText(
                    RepositoryPaths.Template,
                    CanonicalEditorConfig.Translate(File.ReadAllText(RepositoryPaths.Template))
                )
            )
        );

        // ⚠ The population canary. Two empty sets are equal, and an export that resolved to nothing —
        // a moved file, a parse that gave up, a translation that dropped everything — would otherwise
        // pass this loudly. It is worth more than it was: before the rename the export resolved
        // directly, and the only way to reach zero was a broken read. Now it goes through
        // `Translate`, and a `Translate` that matched nothing would produce exactly this shape.
        Assert.NotEmpty(export);
        Assert.Equal(export, own);
    }

    /// <summary>
    ///     A missing configuration and Skala's canonical configuration must give the formatter and
    ///     arranger the same effective values.
    /// </summary>
    /// <remarks>
    ///     ⚠ Compare the built <see cref="FormattingOptions" /> rather than assignments or raw text.
    ///     The repository uses Microsoft aliases for some options and generalized keys can expand into
    ///     several values; neither is visible in a key-by-key comparison of <c>.editorconfig</c> with
    ///     <c>options.json</c>. This is the actual pair of value sets the formatter and arranger read.
    /// </remarks>
    [Fact]
    public void FormatterAndArrangerDefaults_MatchTheRepositoryEditorConfig() {
        var configured = OptionResolver.Resolve(RepositoryPaths.SampleSourceFile).Options;
        var defaults = FormattingOptions.Defaults;
        var mismatches = OptionRegistry.All
            .Select(info => (info.Key, Default: defaults.GetText(info.Id), Configured: configured.GetText(info.Id)))
            .Where(static value => !string.Equals(value.Default, value.Configured, StringComparison.Ordinal))
            .Select(static value => $"{value.Key}: default={value.Default}, .editorconfig={value.Configured}")
            .ToArray();

        Assert.True(
            mismatches.Length == 0,
            "formatter/arranger defaults differ from the repository .editorconfig:\n"
            + string.Join("\n", mismatches)
        );
    }

    /// <summary>Every option a chain configures, as <c>id = value</c>, ordered.</summary>
    /// <remarks>
    ///     ⚠ By <see cref="OptionId" /> rather than by the key that set it. The question is what the
    ///     configuration <em>says</em>, and two files can say the same thing through two spellings.
    /// </remarks>
    static string[] ConfiguredOptions(EditorConfigChain chain) => [
        .. OptionResolver.Resolve(chain)
            .Configured
                .Select(static option => option.Id + " = " + option.Value)
                .OrderBy(static entry => entry, StringComparer.Ordinal)
    ];

    [Theory]
    [InlineData("Foo.cs", true)]
    [InlineData("Foo.csv", false)]
    [InlineData("Foo.txt", false)]
    public void ExtensionGlob_MatchesWhatTheCompilerMatches(string fileName, bool expected) {
        // The 47-extension glob at the bottom of the export is the reason ADR-001 insists on
        // Roslyn's matcher: a hand-rolled one gets {a,b} groups subtly wrong.
        var document = EditorConfigDocument.Load(RepositoryPaths.Template);
        var glob = document.Sections[^1];

        Assert.Equal(expected, SectionMatcher.Matches(glob, Path.Combine(RepositoryPaths.Root, fileName)));
    }

    [Fact]
    public void ChainWalk_StopsAtRoot() {
        var chain = EditorConfigChain.For(RepositoryPaths.SampleSourceFile);

        Assert.True(chain.StoppedAtRoot);
        Assert.Single(chain.Documents);
        Assert.Equal(Path.GetFullPath(RepositoryPaths.EditorConfig), Path.GetFullPath(chain.Documents[0].Path));
    }

    [Fact]
    public void ChainWalk_KeepsClimbingWhenNothingDeclaresRoot() {
        using var tree = new TemporaryTree();
        tree.Write(".editorconfig", "[*]\nindent_size = 2\n");
        tree.Write("nested/.editorconfig", "[*]\nindent_size = 8\n");
        var source = tree.Write("nested/File.cs", "class C;");

        var chain = EditorConfigChain.For(source);

        Assert.False(chain.StoppedAtRoot);
        Assert.Equal(2, chain.Documents.Length);
        // Outermost first, so the nested file is last and therefore wins.
        Assert.EndsWith(Path.Combine("nested", ".editorconfig"), chain.Documents[^1].Path, StringComparison.Ordinal);
    }

    [Fact]
    public void SkalaResolution_AgreesWithTheCompilerOnEveryKeyItOwns() {
        // Skala resolves per option, the compiler per key. Where the compiler has a value for the
        // exact spelling Skala chose as the winner, the two must agree — otherwise Skala's own
        // precedence has drifted from editorconfig's.
        var chain = EditorConfigChain.For(RepositoryPaths.SampleSourceFile);
        var resolution = OptionResolver.Resolve(chain);
        var compiler = SectionMatcher.CompilerView(chain.Documents, RepositoryPaths.SampleSourceFile);

        foreach (var option in resolution.Configured) {
            Assert.True(compiler.TryGetValue(option.Origin!.Spelling, out var value), option.Origin.Spelling);
            Assert.Equal(value, option.Value);
        }
    }
}

/// <summary>A throwaway directory tree, for the chain cases the repository itself cannot show.</summary>
public sealed class TemporaryTree : IDisposable {
    public TemporaryTree() {
        Root = Path.Combine(Path.GetTempPath(), "skala-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string Write(string relativePath, string content) {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose() {
        if (Directory.Exists(Root)) {
            Directory.Delete(Root, true);
        }
    }
}
