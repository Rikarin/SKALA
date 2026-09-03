using System.Collections.Immutable;
using System.Reflection;

namespace Rikarin.Skala.Testing;

/// <summary>One corpus file and the oracle fixtures beside it.</summary>
/// <remarks>
///     ⚠ "the fixture" became "the fixtures" at milestone 4. A file now carries one committed
///     <c>jb cleanupcode</c> output per <see cref="OracleProfile" />: the format-only one every milestone
///     since 1 has measured, and the cleanup one arrangement is measured against. The no-argument
///     members below are the format-only ones, unchanged, so that no existing call site silently starts
///     measuring the other question.
/// </remarks>
public sealed record CorpusFile(string Set, string RelativePath, string Path) {
    /// <summary>The committed format-only <c>jb cleanupcode</c> output.</summary>
    public string ExpectedPath => ExpectedPathFor(OracleProfile.FormatOnly);

    public bool HasFixture => File.Exists(ExpectedPath);

    public string ExpectedPathFor(OracleProfile profile) => System.IO.Path.ChangeExtension(Path, null) + profile.Suffix;

    public bool HasFixtureFor(OracleProfile profile) => File.Exists(ExpectedPathFor(profile));

    public override string ToString() => Set + "/" + RelativePath;
}

/// <summary>
///     <c>Testing/corpus/</c>: the three sets from docs/plan/02 § "The corpus".
/// </summary>
public static class Corpus {
    /// <summary>~1 200 small files, one C# construct each. Every option × every value.</summary>
    public const string Constructs = "constructs";

    /// <summary>Files vendored from real trees, plus a snapshot of Vixen. The fidelity number.</summary>
    public const string Real = "real";

    /// <summary>The formatter's enemies: 4 000-character lines, 30-deep nesting, split <c>#if</c>.</summary>
    public const string Pathological = "pathological";

    /// <summary>
    ///     The subtree of <see cref="Constructs" /> that arrangement owns, and the only part of it that
    ///     carries a cleanup fixture.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not every construct file gets a second fixture. A cleanup fixture costs an oracle run and a
    ///     committed file, and for the 250-odd whitespace constructs the answer is "the same as the
    ///     format-only fixture" — a fixture whose content is predictable measures nothing. The
    ///     arrangement subtree and <see cref="Real" /> are where the second profile has something to say.
    /// </remarks>
    public const string ArrangementPrefix = "arrangement/";

    /// <summary>
    ///     The subtree of <see cref="Constructs" /> that the documentation-comment sub-formatter owns,
    ///     and the only part of the corpus that carries a <see cref="OracleProfile.DocComments" />
    ///     fixture.
    /// </summary>
    /// <remarks>
    ///     ⚠ Same economics as <see cref="ArrangementPrefix" />, and the same reason for the boundary. A
    ///     doc-comment fixture beside a file with no <c>///</c> in it is byte-identical to the
    ///     format-only fixture beside it by construction, which measures nothing and costs an oracle run
    ///     to commit.
    ///     <para>
    ///         ⚠ <see cref="Real" /> is deliberately <em>not</em> in here yet, and that is a scope
    ///         decision rather than a claim that it would say nothing — it would say a great deal, and
    ///         it is what would finally retire the <c>outside doc comments</c> fidelity basis. It is a
    ///         separate reviewed commit of ~700 rewritten fixtures.
    ///     </para>
    /// </remarks>
    public const string XmlDocPrefix = "xmldoc/";

    /// <summary>
    ///     A symbol set that makes a conditional body live, for the properties to be asserted under.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not the oracle's own eighteen, and it does not need to be. What a *fidelity* measurement
    ///     needs is the symbols the oracle actually had (<c>fidelity preprocessor</c> reads them out of
    ///     a binary log for exactly that reason); what a *property* needs is only that <c>#if</c> bodies
    ///     stop being disabled text, because that is the code path the properties were never asserted
    ///     over. A hard-coded list keeps the suite runnable on a machine with no SDK probe and no
    ///     oracle.
    ///     <para>
    ///         ⚠ It lives here rather than in the conformance test project because the fuzzer needs it too
    ///         and the fuzzer is a library, not a test. Two lists would be two answers to "what does
    ///         <c>defined</c> mean", and the second one would drift.
    ///     </para>
    /// </remarks>
    public static readonly ImmutableArray<string> PropertySymbols = [
        "DEBUG",
        "TRACE",
        "NET",
        "NET10_0",
        "NETCOREAPP",
        "NET5_0_OR_GREATER",
        "NET6_0_OR_GREATER",
        "NET7_0_OR_GREATER",
        "NET8_0_OR_GREATER",
        "NET9_0_OR_GREATER",
        "NET10_0_OR_GREATER",
        "HAVE_ASYNC",
        "FEATURE_SPAN"
    ];

    public static string RepositoryRoot { get; } =
        Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(static attribute => attribute.Key == "SkalaRepositoryRoot")?.Value
        ?? throw new InvalidOperationException("SkalaRepositoryRoot was not stamped into the assembly.");

    public static string Root { get; } = Path.Combine(RepositoryRoot, "Testing", "corpus");

    /// <summary>
    ///     The base configuration every oracle run is measured against.
    /// </summary>
    /// <remarks>
    ///     ⚠ One property rather than five spellings of the path. The fixture generator, the variant
    ///     generator and the key-flip sweep each open this file and each records its digest in what they
    ///     commit; a sixth call site that opened a different file would produce fixtures whose recorded
    ///     provenance is a statement about somebody else's configuration.
    ///     <para>
    ///         ⚠ <b>It is no longer <c>&lt;root&gt;/.editorconfig</c>, and the rename from
    ///         <c>BaseEditorConfigPath</c> was so that every call site had to say which of the two it
    ///         meant.</b> See <see cref="OracleEditorConfig" /> for why the oracle must read the export
    ///         rather than the repository's own file. The bytes are unchanged, so the digest is too.
    ///     </para>
    /// </remarks>
    public static string OracleEditorConfigPath => OracleEditorConfig.Path;

    /// <summary>
    ///     The repository's own configuration: what Skala formats Skala with (ADR-015).
    /// </summary>
    /// <remarks>
    ///     ⚠ Never hand this to <c>jb cleanupcode</c>. It is Skala's input, resolved through
    ///     <c>EditorConfigChain</c> by everything that measures Skala's side, and it is free to be
    ///     spelled in a key namespace ReSharper has never heard of.
    /// </remarks>
    public static string RepositoryEditorConfigPath { get; } = Path.Combine(RepositoryRoot, ".editorconfig");

    public static string SetRoot(string set) => Path.Combine(Root, set);

    /// <summary>
    ///     Every committed oracle fixture: <c>*.expected.cs</c> anywhere under the corpus.
    /// </summary>
    /// <remarks>
    ///     ⚠ The filesystem rather than <see cref="All" /> paired with <see cref="OracleProfile" />, and
    ///     the difference is the whole point of the invariant this feeds. A fixture is only reachable
    ///     through a <see cref="CorpusFile" /> if some enumeration still claims it: a fixture whose input
    ///     was renamed, or which belongs to a variant nobody enumerates any more, is exactly the fixture
    ///     whose provenance nothing would ever check. Walking the directory finds those too.
    /// </remarks>
    public static IReadOnlyList<string> Fixtures() =>
        Directory.Exists(Root)
            ? [
                .. Directory.EnumerateFiles(Root, "*.expected.cs", SearchOption.AllDirectories)
                    .OrderBy(static path => path, StringComparer.Ordinal)
            ]
            : [];

    public static IReadOnlyList<CorpusFile> Files(string set) {
        var root = SetRoot(set);
        if (!Directory.Exists(root)) {
            return [];
        }

        return [
            .. Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(static path => !path.EndsWith(".expected.cs", StringComparison.Ordinal))
                .Select(path => new CorpusFile(set, Path.GetRelativePath(root, path).Replace('\\', '/'), path))
                .Where(static file => !IsOpenDefect(file.RelativePath))
                .OrderBy(static file => file.Path, StringComparer.Ordinal)
        ];
    }

    /// <summary>
    ///     <c>pathological/open/</c> — minimised fuzz findings whose defect is not fixed yet.
    /// </summary>
    /// <remarks>
    ///     ⚠ Excluded from every measured set, and the exclusion is the point rather than a dodge. One
    ///     of the entries makes <c>skala format</c> throw an unhandled exception, and a file that throws
    ///     does not fail one assertion — it takes down every harness path that formats the corpus, the
    ///     fidelity number and the differential report included. What holds those files to account
    ///     instead is <c>OpenDefectTests</c>, which asserts that each of them
    ///     <b>
    ///         still fails, in the way
    ///         its register entry records
    ///     </b>: a defect that gets fixed breaks that suite and is told to
    ///     move its file into <c>pathological/</c> proper with an oracle fixture. See
    ///     <c>Testing/corpus/pathological/open/register.md</c>.
    /// </remarks>
    static bool IsOpenDefect(string relativePath) =>
        relativePath.StartsWith(OpenDefects.OpenDirectory + "/", StringComparison.Ordinal);

    public static IReadOnlyList<CorpusFile> All() => [.. Files(Constructs), .. Files(Real), .. Files(Pathological)];

    /// <summary>
    ///     The files a cleanup fixture is expected for: all of <see cref="Real" />, plus the arrangement
    ///     constructs. This is the set <c>./build.sh Oracle</c> regenerates under the second profile and
    ///     the set the M4 differential is measured over.
    /// </summary>
    public static IReadOnlyList<CorpusFile> Arrangeable() => [.. ArrangementConstructs(), .. Files(Real)];

    /// <summary>
    ///     The arrangement half of <see cref="Arrangeable" />: <c>constructs/arrangement/</c> alone.
    /// </summary>
    /// <remarks>
    ///     ⚠ A set rather than an inline <c>Where</c>, because it is a <em>compilation</em> and not just
    ///     a list of files. <c>usings/sort-and-remove.cs</c> imports <c>Alpha.Things</c>, which exists
    ///     only because <c>usings/namespaces.cs</c> declares it; drop either file and the import stops
    ///     resolving, whereupon both engines answer a different question about
    ///     <c>resharper_sort_usings</c> than the one that was asked. The key-flip sweep gives its oracle
    ///     this whole subtree and compiles Skala's side over the same one, so that the two are asked the
    ///     same question — the argument <c>ArrangementDifferential.ImplicitUsings</c> makes about the
    ///     SDK's global usings, applied to the corpus's own cross-references.
    ///     <para>
    ///         ⚠ <see cref="Real" /> is deliberately not in here. The differential measures both; the
    ///         sweep runs ~270 oracle configurations and cannot carry 380 vendored files into every one.
    ///         Nothing under <c>constructs/arrangement/</c> references anything under <see cref="Real" />.
    ///     </para>
    /// </remarks>
    public static IReadOnlyList<CorpusFile> ArrangementConstructs() => [
        .. Files(Constructs)
            .Where(static file => file.RelativePath.StartsWith(ArrangementPrefix, StringComparison.Ordinal))
    ];

    /// <summary>
    ///     The files a documentation-comment fixture is expected for: <c>constructs/xmldoc/</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the set <c>./build.sh Oracle</c> regenerates under
    ///     <see cref="OracleProfile.DocComments" />, and the set <c>XmlDocOracleTests</c> compares Skala
    ///     against. It is what makes the <c>resharper_xmldoc_*</c> family measurable at all.
    /// </remarks>
    public static IReadOnlyList<CorpusFile> DocCommented() => [
        .. Files(Constructs)
            .Where(static file => file.RelativePath.StartsWith(XmlDocPrefix, StringComparison.Ordinal))
    ];

    /// <summary>xUnit theory data: one row per file in a set.</summary>
    public static IEnumerable<object[]> TheoryData(string set) =>
        Files(set).Select(static file => new object[] { file });
}
