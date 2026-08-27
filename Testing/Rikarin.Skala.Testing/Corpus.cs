using System.Reflection;

namespace Rikarin.Skala.Testing;

/// <summary>One corpus file and the oracle fixtures beside it.</summary>
/// <remarks>
/// ⚠ "the fixture" became "the fixtures" at milestone 4. A file now carries one committed
/// <c>jb cleanupcode</c> output per <see cref="OracleProfile"/>: the format-only one every milestone
/// since 1 has measured, and the cleanup one arrangement is measured against. The no-argument
/// members below are the format-only ones, unchanged, so that no existing call site silently starts
/// measuring the other question.
/// </remarks>
public sealed record CorpusFile(string Set, string RelativePath, string Path) {
    /// <summary>The committed format-only <c>jb cleanupcode</c> output.</summary>
    public string ExpectedPath => ExpectedPathFor(OracleProfile.FormatOnly);

    public bool HasFixture => File.Exists(ExpectedPath);

    public string ExpectedPathFor(OracleProfile profile) =>
        System.IO.Path.ChangeExtension(Path, null) + profile.Suffix;

    public bool HasFixtureFor(OracleProfile profile) => File.Exists(ExpectedPathFor(profile));

    public override string ToString() => Set + "/" + RelativePath;
}

/// <summary>
/// <c>Testing/corpus/</c>: the three sets from docs/plan/02 § "The corpus".
/// </summary>
public static class Corpus {
    /// <summary>~1 200 small files, one C# construct each. Every option × every value.</summary>
    public const string Constructs = "constructs";

    /// <summary>Files vendored from real trees, plus a snapshot of Vixen. The fidelity number.</summary>
    public const string Real = "real";

    /// <summary>The formatter's enemies: 4 000-character lines, 30-deep nesting, split <c>#if</c>.</summary>
    public const string Pathological = "pathological";

    /// <summary>
    /// The subtree of <see cref="Constructs"/> that arrangement owns, and the only part of it that
    /// carries a cleanup fixture.
    /// </summary>
    /// <remarks>
    /// ⚠ Not every construct file gets a second fixture. A cleanup fixture costs an oracle run and a
    /// committed file, and for the 250-odd whitespace constructs the answer is "the same as the
    /// format-only fixture" — a fixture whose content is predictable measures nothing. The
    /// arrangement subtree and <see cref="Real"/> are where the second profile has something to say.
    /// </remarks>
    public const string ArrangementPrefix = "arrangement/";

    public static string RepositoryRoot { get; } =
        Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(static attribute => attribute.Key == "SkalaRepositoryRoot")?.Value
        ?? throw new InvalidOperationException("SkalaRepositoryRoot was not stamped into the assembly.");

    public static string Root { get; } = Path.Combine(RepositoryRoot, "Testing", "corpus");

    public static string SetRoot(string set) => Path.Combine(Root, set);

    public static IReadOnlyList<CorpusFile> Files(string set) {
        var root = SetRoot(set);
        if (!Directory.Exists(root)) {
            return [];
        }

        return [
            .. Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(static path => !path.EndsWith(".expected.cs", StringComparison.Ordinal))
                .OrderBy(static path => path, StringComparer.Ordinal)
                .Select(path => new CorpusFile(set, Path.GetRelativePath(root, path).Replace('\\', '/'), path))
        ];
    }

    public static IReadOnlyList<CorpusFile> All() => [.. Files(Constructs), .. Files(Real), .. Files(Pathological)];

    /// <summary>
    /// The files a cleanup fixture is expected for: all of <see cref="Real"/>, plus the arrangement
    /// constructs. This is the set <c>./build.sh Oracle</c> regenerates under the second profile and
    /// the set the M4 differential is measured over.
    /// </summary>
    public static IReadOnlyList<CorpusFile> Arrangeable() => [
        .. Files(Constructs)
            .Where(static file => file.RelativePath.StartsWith(ArrangementPrefix, StringComparison.Ordinal)),
        .. Files(Real)
    ];

    /// <summary>xUnit theory data: one row per file in a set.</summary>
    public static IEnumerable<object[]> TheoryData(string set) =>
        Files(set).Select(static file => new object[] { file });
}
