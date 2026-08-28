using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Core.Tests;

/// <summary>
///     The one predicate behind every whole-tree walk, and the reason CI could not go green.
/// </summary>
/// <remarks>
///     ⚠ The assertions here are about a *measured* failure, not a hypothetical one. This repository's
///     three data directories put 1 924 <c>.cs</c> files into the coverage denominator behind
///     <c>--require-fresh-binlog</c>; it read 294 of 2 220 — 13 % — against a binlog that had compiled
///     everything there was to compile, and every push to <c>master</c> exited 4.
/// </remarks>
public sealed class SourceExclusionsTests {
    static string Under(params string[] segments) => Path.Combine(RepositoryPaths.Root, Path.Combine(segments));

    static bool ExcludedFromRoot(SourceExclusions exclusions, params string[] segments) {
        var full = Under(segments);
        return exclusions.Excludes(Path.GetRelativePath(RepositoryPaths.Root, full), full);
    }

    /// <summary>
    ///     ⚠ Reads this repository's real <c>skala.jsonc</c>. A test against a synthesised one would
    ///     pass while the committed file said something else, which is exactly the failure that shipped.
    /// </summary>
    [Fact]
    public void TheRepositorysOwnDataDirectories_AreExcluded() {
        var exclusions = SourceExclusions.For(RepositoryPaths.Root);

        Assert.True(ExcludedFromRoot(exclusions, "Testing", "corpus", "real", "Sample.cs"));
        Assert.True(ExcludedFromRoot(exclusions, "Testing", "corpus", "pathological", "goto-and-labels.cs"));
        Assert.True(ExcludedFromRoot(exclusions, "Rules", "Rikarin.Skala.Rules.Tests", "fixtures", "SK1010", "a.cs"));
        Assert.True(ExcludedFromRoot(exclusions, "Rules", "Rikarin.Skala.Rules.Tests", "corpus", "vulnerable", "a.cs"));
    }

    /// <summary>
    ///     ⚠ The discriminating half. An exclusion that also swallowed the tool's own sources would take
    ///     the coverage ratio to 100 % by analysing nothing, which is the failure the ratio exists to
    ///     catch and would be reported as success.
    /// </summary>
    [Fact]
    public void TheToolsOwnSources_AreNot() {
        var exclusions = SourceExclusions.For(RepositoryPaths.Root);

        Assert.False(ExcludedFromRoot(exclusions, "Core", "Rikarin.Skala.Core", "SkalaDirectory.cs"));
        Assert.False(ExcludedFromRoot(exclusions, "Rules", "Rikarin.Skala.Rules.Tests", "RuleFixtures.cs"));
        Assert.False(ExcludedFromRoot(exclusions, "Testing", "Rikarin.Skala.Testing", "CorpusSample.cs"));
        Assert.False(ExcludedFromRoot(exclusions, "build", "Build.cs"));
    }

    [Theory]
    [InlineData("obj")]
    [InlineData("bin")]
    [InlineData(".git")]
    [InlineData(".claude")]
    [InlineData("artifacts")]
    [InlineData(".skala")]
    public void TheBuiltInDirectories_NeedNoConfiguration(string directory) {
        var relative = Path.Combine("Core", directory, "Whatever.cs");
        Assert.True(SourceExclusions.BuiltIn.Excludes(relative, Path.Combine(RepositoryPaths.Root, relative)));
    }

    /// <summary>
    ///     ⚠ <b>The worktree case, and it is the one that has actually destroyed work.</b> An agent
    ///     worktree lives at <c>&lt;repo&gt;/.claude/worktrees/&lt;id&gt;/</c>, so every absolute path
    ///     inside one contains <c>.claude</c>. Testing the absolute path would refuse to look at
    ///     anything at all while working in a worktree — silently — and testing the relative path is
    ///     what stops a sweep from *above* reaching into one. One run that got this wrong the other way
    ///     rewrote 2 796 files inside another agent's worktree while that agent was working in it.
    /// </summary>
    [Fact]
    public void APathBelowAWorktreeRoot_IsReached_WhileASweepFromAboveIsNot() {
        var worktree = Path.Combine(RepositoryPaths.Root, ".claude", "worktrees", "agent-1");
        var file = Path.Combine(worktree, "Core", "Thing.cs");

        // Walking the worktree itself: the file is below the named root and is reached.
        Assert.False(SourceExclusions.BuiltIn.Excludes(Path.GetRelativePath(worktree, file), file));

        // Sweeping from the repository root: the same file is refused.
        Assert.True(SourceExclusions.BuiltIn.Excludes(Path.GetRelativePath(RepositoryPaths.Root, file), file));
    }

    /// <summary>
    ///     ⚠ A declared pattern is anchored to the repository, not to whatever directory the caller
    ///     named, so <c>skala format Testing</c> honours <c>Testing/corpus/**</c> exactly as
    ///     <c>skala format .</c> does. Matching it relative to the walk root instead would mean the
    ///     corpus was excluded from one invocation and reformatted by the other, which is worse than
    ///     either answer taken consistently.
    /// </summary>
    [Fact]
    public void ADeclaredPattern_IsAnchoredToTheRepository_NotToTheWalkRoot() {
        var exclusions = SourceExclusions.For(RepositoryPaths.Root);
        var walkRoot = Path.Combine(RepositoryPaths.Root, "Testing");
        var file = Path.Combine(walkRoot, "corpus", "real", "Sample.cs");

        Assert.True(exclusions.Excludes(Path.GetRelativePath(walkRoot, file), file));
    }

    [Fact]
    public void TheCommittedConfiguration_DeclaresTheThreeDataDirectories() {
        var configuration = ToolConfiguration.Find(RepositoryPaths.Root);

        Assert.NotNull(configuration);
        Assert.Equal(
            [
                "Testing/corpus/**",
                "Rules/Rikarin.Skala.Rules.Tests/fixtures/**",
                "Rules/Rikarin.Skala.Rules.Tests/corpus/**"
            ],
            configuration.Exclude
        );
    }

    /// <summary>
    ///     ⚠ A repository with no <c>skala.jsonc</c>, and one whose <c>exclude</c> is absent or is not an
    ///     array, gets the built-in directories and no more — never an empty walk. A walker that excluded
    ///     everything on a malformed configuration would report a clean tree.
    /// </summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "exclude": [] }""")]
    [InlineData("""{ "exclude": "Testing/corpus/**" }""")]
    [InlineData("""{ "exclude": [17, null, { "path": "x" }] }""")]
    public void AConfigurationWithNoUsablePatterns_ExcludesNothingExtra(string json) {
        var configuration = ToolConfiguration.FromText(Path.Combine(RepositoryPaths.Root, "skala.jsonc"), json);
        Assert.Empty(configuration.Exclude);
    }
}
