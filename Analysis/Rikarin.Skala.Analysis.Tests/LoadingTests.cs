using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>A scratch directory of C# files, for the loose path an agent actually uses.</summary>
public sealed class Scratch : IDisposable {
    public Scratch() {
        Root = Directory.CreateTempSubdirectory("skala-analysis-").FullName;
    }

    public string Root { get; }

    public string Write(string name, string content) {
        var path = Path.Combine(Root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    readonly List<string> locked = [];

    /// <summary>
    ///     Writes a file this process may not read, or returns null when this machine cannot make one.
    /// </summary>
    /// <remarks>
    ///     ⚠ Decided by <em>attempting the read</em>, not by platform or uid. Windows has no mode bit,
    ///     but the case that makes a fixture lie is <b>root</b>, which opens a mode-000 file happily
    ///     and is the default in a CI container; a test that cannot go red there is the defect. The
    ///     bits are restored in <see cref="Dispose" /> so teardown never depends on them.
    /// </remarks>
    public string? WriteUnreadable(string name, string content) {
        if (OperatingSystem.IsWindows()) {
            return null;
        }

        var path = Write(name, content);
        File.SetUnixFileMode(path, UnixFileMode.None);
        locked.Add(path);

        try {
            File.ReadAllText(path);
            return null;
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            return path;
        }
    }

    /// <summary>
    ///     Makes a file from <see cref="WriteUnreadable" /> readable again, for the control half of a
    ///     fixture: the same command over the same tree with the bits restored is the run the locked
    ///     one is compared against.
    /// </summary>
    public static void Unlock(string path) {
        if (!OperatingSystem.IsWindows()) {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead
            );
        }
    }

    public void Dispose() {
        // `locked` is empty on Windows; the guard is for CA1416, which cannot see that.
        if (!OperatingSystem.IsWindows()) {
            foreach (var path in locked) {
                try {
                    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
                    // The directory delete below does not need the file's own bits; this is courtesy.
                }
            }
        }

        try {
            Directory.Delete(Root, true);
        } catch (IOException) {
            // A scratch directory under the temp directory that will not delete — a handle another
            // process still holds, most often — is not a test result. Failing the test here would
            // report a finding that does not exist; the OS reclaims the directory either way.
        }
    }
}

/// <summary>docs/plan/07's three load modes.</summary>
public sealed class LoadingTests {
    [Fact]
    public void Loose_BuildsACompilationFromFilesWithNoProject() {
        using var scratch = new Scratch();
        scratch.Write("Foo.cs", "public sealed class Foo { public int Value; }");
        scratch.Write("Bar.cs", "public sealed class Bar { public Foo Child = new(); }");

        var loaded = ProjectLoader.Load(
            new LoadRequest { RepositoryRoot = scratch.Root, Mode = LoadMode.Loose },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(LoadMode.Loose, loaded.Mode);
        var unit = Assert.Single(loaded.Units);
        Assert.Equal(2, unit.Compilation.SyntaxTrees.Length);
        Assert.Equal(2, unit.ReportablePaths.Count);
    }

    /// <summary>
    ///     ⚠ The framework is referenced, so BCL types resolve even with no project. That is what makes
    ///     the loose mode's syntactic rules trustworthy rather than "silent because nothing bound".
    /// </summary>
    [Fact]
    public void Loose_ResolvesTheBclEvenWithNoProject() {
        using var scratch = new Scratch();
        var path = scratch.Write("Foo.cs", "public sealed class Foo { public string? Name; }");

        var loaded = ProjectLoader.Load(
            new LoadRequest { RepositoryRoot = scratch.Root, Mode = LoadMode.Loose },
            TestContext.Current.CancellationToken
        );
        var compilation = loaded.Units[0].Compilation;

        Assert.NotNull(compilation.GetTypeByMetadataName("System.String"));
        Assert.NotNull(compilation.GetTypeByMetadataName("System.ArgumentNullException"));
        Assert.Equal(path, loaded.Units[0].ReportablePaths.Single());
    }

    [Fact]
    public void Loose_TakesThePreprocessorSymbolsItIsGiven() {
        using var scratch = new Scratch();
        scratch.Write("Foo.cs", "public sealed class Foo {\n#if DEBUG\n    public int Debug;\n#endif\n}");

        var loaded = ProjectLoader.Load(
            new LoadRequest { RepositoryRoot = scratch.Root, Mode = LoadMode.Loose, Define = ["DEBUG"] },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(["DEBUG"], loaded.Units[0].PreprocessorSymbols);
        Assert.Contains(
            "Debug",
            loaded.Units[0].Compilation.SyntaxTrees.First()
                .GetRoot(TestContext.Current.CancellationToken)
                .DescendantTokens()
                .Select(static token => token.ValueText)
        );
    }

    /// <summary>
    ///     ⚠ Falling through is allowed and is reported; falling through <em>silently</em> is not. A
    ///     loose result must never be mistakable for a binlog one.
    /// </summary>
    [Fact]
    public void Binlog_WithNoBinlog_FallsThroughToLooseAndSaysSo() {
        using var scratch = new Scratch();
        scratch.Write("Foo.cs", "public sealed class Foo;");

        var loaded = ProjectLoader.Load(
            new LoadRequest { RepositoryRoot = scratch.Root, Mode = LoadMode.Binlog },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(LoadMode.Loose, loaded.Mode);
        Assert.Contains(loaded.Diagnostics, static diagnostic => diagnostic.Id == "SK9022");
        Assert.Contains("loose", loaded.Summary, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ Asking for loose is asking for speed and for the semantics-free rule set. Quietly running a
    ///     build's worth of work instead would blow the budget the mode exists to meet.
    /// </summary>
    [Fact]
    public void Loose_NeverFallsUpwards() {
        using var scratch = new Scratch();
        scratch.Write("Foo.cs", "public sealed class Foo;");

        var loaded = ProjectLoader.Load(
            new LoadRequest { RepositoryRoot = scratch.Root, Mode = LoadMode.Loose },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(LoadMode.Loose, loaded.Mode);
        Assert.DoesNotContain(loaded.Diagnostics, static diagnostic => diagnostic.Id == "SK9022");
    }

    [Fact]
    public void Binlog_WithNoFallbackAllowed_ReturnsNothing() {
        using var scratch = new Scratch();
        scratch.Write("Foo.cs", "public sealed class Foo;");

        var loaded = ProjectLoader.Load(
            new LoadRequest { RepositoryRoot = scratch.Root, Mode = LoadMode.Binlog, AllowFallback = false },
            TestContext.Current.CancellationToken
        );

        Assert.True(loaded.IsEmpty);
        Assert.Equal(LoadMode.Binlog, loaded.Mode);
    }

    [Fact]
    public void GeneratedFiles_AreAnalysedAndNeverReportedOn() {
        using var scratch = new Scratch();
        scratch.Write("Foo.cs", "public sealed partial class Foo;");
        scratch.Write("Foo.g.cs", "public sealed partial class Foo { public int Generated; }");

        var loaded = ProjectLoader.Load(
            new LoadRequest { RepositoryRoot = scratch.Root, Mode = LoadMode.Loose },
            TestContext.Current.CancellationToken
        );
        var unit = loaded.Units[0];

        Assert.Equal(2, unit.Compilation.SyntaxTrees.Length);
        Assert.Single(unit.ReportablePaths);
        Assert.EndsWith("Foo.cs", unit.ReportablePaths.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void MetadataReferences_AreCachedAcrossCompilations() {
        MetadataReferenceCache.Clear();
        using var scratch = new Scratch();
        scratch.Write("Foo.cs", "public sealed class Foo;");

        ProjectLoader.Load(
            new LoadRequest { RepositoryRoot = scratch.Root, Mode = LoadMode.Loose },
            TestContext.Current.CancellationToken
        );
        var misses = MetadataReferenceCache.Misses;

        ProjectLoader.Load(
            new LoadRequest { RepositoryRoot = scratch.Root, Mode = LoadMode.Loose },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(misses, MetadataReferenceCache.Misses);
    }

    [Theory]
    [InlineData("binlog", LoadMode.Binlog)]
    [InlineData("workspace", LoadMode.Workspace)]
    [InlineData("loose", LoadMode.Loose)]
    public void LoadModes_ParseTheSpellingsTheDocumentUses(string text, LoadMode expected) {
        Assert.True(LoadModes.TryParse(text, out var mode));
        Assert.Equal(expected, mode);
    }

    [Fact]
    public void LoadModes_RejectAnythingElse() => Assert.False(LoadModes.TryParse("magic", out _));
}
