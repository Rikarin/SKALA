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
        Assert.Equal(4226, document.Assignments.Count());
        Assert.False(document.IsRoot);
    }

    [Fact]
    public void RepositoryEditorConfig_IsTheTemplateWithRootAdded() {
        // ADR-015 — Skala formats Skala, and the configuration it formats itself with is the export.
        var template = File.ReadAllText(RepositoryPaths.Template);
        var own = File.ReadAllText(RepositoryPaths.EditorConfig);

        Assert.StartsWith("root = true", own, StringComparison.Ordinal);
        Assert.EndsWith(template, own, StringComparison.Ordinal);
        Assert.True(EditorConfigDocument.Load(RepositoryPaths.EditorConfig).IsRoot);
    }

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
            Directory.Delete(Root, recursive: true);
        }
    }
}
