using Rikarin.Skala.Formatting.CSharp;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     What a recursive walk is allowed to reach.
/// </summary>
/// <remarks>
///     ⚠ These assert a <b>containment</b> property rather than a formatting one, and the one that
///     wanted them cost 2 796 files. An agent worktree is a second checkout of this repository inside
///     it — the repository's own <c>.gitignore</c> says so, above the <c>.claude/worktrees/</c> line —
///     and git honours that while a walk over <see cref="SearchOption.AllDirectories" /> does not. A
///     single <c>skala format --xmldoc</c> from the main checkout therefore descended into another
///     agent's worktree and re-indented every documentation comment in it, in files that agent had
///     never opened, while it was working there.
///     <para>
///         ⚠ The second test is the more important one. The obvious fix — testing the absolute path for
///         <c>.claude</c> — refuses to format anything at all from inside a worktree, because every
///         absolute path in one contains it, and refuses it <em>silently</em>: <c>./build.sh Lint</c> would
///         have gone green over zero files in every worktree in the repository. The exclusion is on the
///         walk, not on the file.
///     </para>
/// </remarks>
public sealed class FileCollectionTests : IDisposable {
    readonly string _root = Directory.CreateTempSubdirectory("skala-collect-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    string Write(string relative, string content) {
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ASweepFromAbove_DoesNotDescendIntoAnAgentWorktree() {
        var outside = Write("Normal/Outside.cs", "class B { }\n");
        Write(".claude/worktrees/agent-x/Inside.cs", "class A { }\n");
        Write("obj/Generated.cs", "class G { }\n");
        Write("bin/Built.cs", "class D { }\n");

        Assert.Equal([outside], FormatCommand.Collect([_root]));
    }

    /// <summary>
    ///     ⚠ Naming a worktree, or working inside one, still reaches its files.
    /// </summary>
    [Fact]
    public void NamingAWorktree_StillReachesItsFiles() {
        var inside = Write(".claude/worktrees/agent-x/Inside.cs", "class A { }\n");
        var worktree = Path.GetDirectoryName(inside)!;

        Assert.Equal([inside], FormatCommand.Collect([worktree]));
        Assert.Equal([inside], FormatCommand.Collect([inside]));
    }
}
