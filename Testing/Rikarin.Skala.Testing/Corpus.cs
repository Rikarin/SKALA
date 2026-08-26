using System.Reflection;

namespace Rikarin.Skala.Testing;

/// <summary>One corpus file and the oracle fixture beside it.</summary>
public sealed record CorpusFile(string Set, string RelativePath, string Path) {
    /// <summary>The committed <c>jb cleanupcode</c> output, or null when there is none yet.</summary>
    public string ExpectedPath => System.IO.Path.ChangeExtension(Path, null) + ".expected.cs";

    public bool HasFixture => File.Exists(ExpectedPath);

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

        return [.. Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.EndsWith(".expected.cs", StringComparison.Ordinal))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .Select(path => new CorpusFile(set, Path.GetRelativePath(root, path).Replace('\\', '/'), path))];
    }

    public static IReadOnlyList<CorpusFile> All() => [.. Files(Constructs), .. Files(Real), .. Files(Pathological)];

    /// <summary>xUnit theory data: one row per file in a set.</summary>
    public static IEnumerable<object[]> TheoryData(string set) =>
        Files(set).Select(static file => new object[] { file });
}
