using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>A scratch directory of C# files, for the loose path an agent actually uses.</summary>
public sealed class Scratch : IDisposable {
    public Scratch() => Root = Directory.CreateTempSubdirectory("skala-analysis-").FullName;

    public string Root { get; }

    public string Write(string name, string content) {
        var path = Path.Combine(Root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose() {
        try {
            Directory.Delete(Root, recursive: true);
        } catch (IOException) { }
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
        Assert.Contains(loaded.Diagnostics, diagnostic => diagnostic.Id == "SK9022");
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
        Assert.DoesNotContain(loaded.Diagnostics, diagnostic => diagnostic.Id == "SK9022");
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
