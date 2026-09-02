using System.Xml.Linq;

namespace Rikarin.Skala.Core.Tests;

/// <summary>
///     The forbidden edges from docs/plan/02-repository-layout.md § "The project graph".
/// </summary>
/// <remarks>
///     ⚠ These are written against the projects that exist today and fail loudly when a project the
///     plan forbids referencing appears and is referenced. The plan is explicit that the graph is
///     "enforced by a test that walks the dependency closure, not by discipline" — a forbidden edge
///     added in Milestone 4 must break this test, not a user's analyzer load.
/// </remarks>
public sealed class ProjectGraphTests {
    static IReadOnlyList<ProjectFile> Projects { get; } = ProjectFile.LoadAll(RepositoryPaths.Root);

    [Fact]
    public void EveryProjectInTheTree_IsInTheSolution() {
        var solution = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "Skala.slnx"));
        foreach (var project in Projects.Where(static p => p.Name != "_build")) {
            Assert.Contains(project.Name + ".csproj", solution, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NothingReferencesTheCli() {
        // A tool whose logic lives in its entry-point assembly cannot be embedded, and embedding is
        // exactly what MSBuild and MCP need. A build-order edge (ReferenceOutputAssembly="false")
        // creates no assembly reference and is allowed; anything that would let a caller `using
        // Rikarin.Skala.Cli` is not.
        foreach (var project in Projects) {
            foreach (var reference in project.ProjectReferences.Where(static r => r.Path.EndsWith(
                             "Rikarin.Skala.Cli.csproj",
                             StringComparison.Ordinal
                         )
                     )) {
                Assert.False(
                    reference.ReferencesOutputAssembly,
                    $"{project.Name} takes a compile-time reference on the CLI."
                );
            }
        }

        var sources = Directory.EnumerateFiles(RepositoryPaths.Root, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !ProjectFile.IsScratch(RepositoryPaths.Root, path))
            .Where(static path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}Rikarin.Skala.Cli{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal
                )
            );

        foreach (var source in sources) {
            foreach (var line in File.ReadLines(source)) {
                Assert.NotEqual("using Rikarin.Skala.Cli;", line.Trim());
            }
        }
    }

    /// <summary>
    ///     ⚠ The graph tests are all of the form "no project has edge X", and every one of them passes
    ///     over an empty project list. This asserts the list is not empty, so that a filter that
    ///     excludes the whole tree — which is exactly what happened from inside an agent worktree —
    ///     fails here with a comprehensible message instead of as three unrelated `Assert.Single`s.
    /// </summary>
    [Fact]
    public void TheProjectGraph_IsNotEmpty() {
        Assert.True(
            Projects.Count > 10,
            $"Only {Projects.Count} project(s) found under {RepositoryPaths.Root}. "
            + "Every other test in this class passes vacuously when this is empty."
        );

        Assert.Contains(Projects, static project => project.Name == "Rikarin.Skala.Core");
    }

    [Fact]
    public void FormattingKnowsNothingAboutCSharp() {
        // ⚠ The IR and the fitting algorithm are what the HTML and CSS front ends reuse
        // (docs/plan/14). The moment SyntaxKind appears in Rikarin.Skala.Formatting the
        // language-plugin seam is gone, and it goes quietly: the project would still build.
        var formatting = Assert.Single(Projects, static p => p.Name == "Rikarin.Skala.Formatting");

        Assert.DoesNotContain(
            formatting.PackageReferences,
            static package => package.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            formatting.ProjectReferences,
            static reference => reference.Path.Contains("CSharp", StringComparison.Ordinal)
        );

        // A package reference is the obvious edge; a transitive one through Core is the edge that
        // gets added by accident, so the whole closure is walked rather than the direct list.
        foreach (var reference in Closure(formatting)) {
            Assert.DoesNotContain(
                reference.PackageReferences,
                static package => package.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)
            );
        }

        // And the source itself: a `using Microsoft.CodeAnalysis` would not compile today, but a
        // hand-rolled `SyntaxKind` copy would, and it would be worse.
        var directory = System.IO.Path.GetDirectoryName(formatting.Path)!;
        foreach (var source in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)) {
            if (ProjectFile.IsScratch(RepositoryPaths.Root, source)) {
                continue;
            }

            // Comments may name Roslyn — one of them explains why this project may not use it.
            foreach (var line in File.ReadLines(source)) {
                var code = line.TrimStart();
                if (code.StartsWith("//", StringComparison.Ordinal)
                    || code.StartsWith("///", StringComparison.Ordinal)) {
                    continue;
                }

                Assert.DoesNotContain("Microsoft.CodeAnalysis", line, StringComparison.Ordinal);
                Assert.DoesNotContain("SyntaxKind", line, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void TheHarnessIsWhereTheTestsGetTheirCliRunner() {
        // docs/plan/02 § "The project graph": tests reference a CliRunner in Testing, not the CLI.
        var testing = Assert.Single(Projects, static p => p.Name == "Rikarin.Skala.Testing");
        Assert.All(
            testing.ProjectReferences.Where(static r => r.Path.EndsWith(
                    "Rikarin.Skala.Cli.csproj",
                    StringComparison.Ordinal
                )
            ),
            static reference => Assert.False(reference.ReferencesOutputAssembly)
        );

        Assert.True(
            File.Exists(Path.Combine(Path.GetDirectoryName(testing.Path)!, "CliRunner.cs")),
            "CliRunner belongs in Rikarin.Skala.Testing (docs/plan/02), not in a test project."
        );

        foreach (var project in Projects.Where(static p => p.Name.EndsWith(".Tests", StringComparison.Ordinal))) {
            var directory = Path.GetDirectoryName(project.Path)!;
            Assert.False(
                File.Exists(Path.Combine(directory, "CliRunner.cs")),
                $"{project.Name} has its own CliRunner; there is one, and it is in Rikarin.Skala.Testing."
            );
        }
    }

    /// <summary>Every project reachable from <paramref name="project" /> by assembly references.</summary>
    static IEnumerable<ProjectFile> Closure(ProjectFile project) {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<ProjectFile>();
        queue.Enqueue(project);

        while (queue.Count > 0) {
            var current = queue.Dequeue();
            foreach (var reference in current.ProjectReferences.Where(static r => r.ReferencesOutputAssembly)) {
                var name = Path.GetFileNameWithoutExtension(reference.Path);
                if (!seen.Add(name)) {
                    continue;
                }

                var target = Projects.FirstOrDefault(p => p.Name == name);
                if (target is not null) {
                    yield return target;
                    queue.Enqueue(target);
                }
            }
        }
    }

    [Fact]
    public void TheAnalyzerPackageReferencesOnlyRoslynAndItsMetadata() {
        // ⚠ Rikarin.Skala.Rules arrived at Milestone 5, so this now guards the real analyzer package
        // as well as the two generators. `.Rules.Metadata` is deliberately outside the filter: it is
        // netstandard2.0 for the same reason but is an ordinary library rather than a Roslyn
        // component, and its own reference set is checked by being the only thing Rules may name.
        foreach (var project in Projects.Where(static p => p.Name.EndsWith(".Rules", StringComparison.Ordinal)
                     || p.Name.EndsWith(
                         ".Generator",
                         StringComparison.Ordinal
                     )
                 )) {
            Assert.Equal("netstandard2.0", project.TargetFramework);

            foreach (var package in project.PackageReferences) {
                Assert.True(
                    package.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal),
                    $"{project.Name} references '{package}'. An analyzer loads into csc and into Rider, and a transitive reference that is not netstandard2.0 fails the load with an error message that names none of it."
                );
            }

            foreach (var reference in project.ProjectReferences.Where(static r => r.ReferencesOutputAssembly)) {
                Assert.True(
                    reference.Path.EndsWith("Rules.Metadata.csproj", StringComparison.Ordinal),
                    $"{project.Name} references '{reference.Path}'."
                );
            }
        }
    }

    [Fact]
    public void CoreDoesNotReachIntoTheToolLayer() {
        var core = Assert.Single(Projects, static p => p.Name == "Rikarin.Skala.Core");
        Assert.All(
            core.ProjectReferences,
            static reference => Assert.Contains("Rikarin.Skala.Options", reference.Path, StringComparison.Ordinal)
        );
    }
}

/// <summary>A ProjectReference, and whether it creates an assembly reference or only build order.</summary>
public sealed record ProjectDependency(string Path, bool ReferencesOutputAssembly);

public sealed record ProjectFile(
    string Name,
    string Path,
    string? TargetFramework,
    IReadOnlyList<string> PackageReferences,
    IReadOnlyList<ProjectDependency> ProjectReferences) {
    /// <summary>
    ///     Every project of the repository, from the file system.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>.claude/worktrees/</c> is excluded, and it is not housekeeping. An agent worktree is a
    ///     second checkout of this repository <em>inside</em> it, so every project appears twice and
    ///     `Assert.Single` fails on a tree nobody edited — a gate that goes red because somebody else
    ///     started a task is not a gate. It is the same mistake the Vixen corpus sample made, one
    ///     directory up.
    /// </remarks>
    public static IReadOnlyList<ProjectFile> LoadAll(string root) =>
        Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsScratch(root, path))
            .Select(Load)
            .OrderBy(static project => project.Name, StringComparer.Ordinal)
            .ToArray();

    /// <summary>Build output, and any checkout of this repository nested inside it.</summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         Matched against the path relative to <paramref name="root" />, and it used to be matched
    ///         against the absolute path.
    ///     </b> That worked from a normal checkout and broke completely from
    ///     inside an agent worktree, which lives at <c>&lt;repo&gt;/.claude/worktrees/&lt;name&gt;/</c> —
    ///     every absolute path under one contains <c>/.claude/</c>, so every project was excluded,
    ///     <see cref="LoadAll" /> returned nothing, and three <c>Assert.Single</c> calls failed on a tree
    ///     nobody had touched. The exclusion is right; the frame of reference was not. Same bug, and the
    ///     same fix, as <c>ToolDiagnosticIdTests.SourceFiles</c>.
    /// </remarks>
    internal static bool IsScratch(string root, string path) {
        var separator = System.IO.Path.DirectorySeparatorChar;
        var relative = separator + System.IO.Path.GetRelativePath(root, path) + separator;
        return relative.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
            || relative.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
            || relative.Contains($"{separator}.claude{separator}", StringComparison.Ordinal)
            // ⚠ `artifacts/` is build output and never source. It was missing here, and
            // `./build.sh ReleasePlan` briefly materialised the previous release's whole checkout
            // into it — at which point `LoadAll` found two `Rikarin.Skala.Core` and every
            // `Assert.Single` in this class failed at once. The scratch moved out of the repository
            // (docs/plan/18, `Build.ReleaseScratch`), and this stays so that the next thing to
            // publish into `artifacts/` does not rediscover it. `EnumerateSources` already excludes
            // it, which is the same rule stated in the other half of the tree.
            || relative.Contains($"{separator}artifacts{separator}", StringComparison.Ordinal);
    }

    static ProjectFile Load(string path) {
        var document = XDocument.Load(path);
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        var packages = document.Descendants("PackageReference")
            .Select(static element => element.Attribute("Include")?.Value
                ?? element.Attribute("Update")?.Value
                ?? string.Empty
            )
            .Where(static value => value.Length > 0 && !value.StartsWith("@(", StringComparison.Ordinal))
            .ToArray();
        var projects = document.Descendants("ProjectReference")
            .Select(static element => new ProjectDependency(
                    (element.Attribute("Include")?.Value ?? string.Empty).Replace(
                        '\\',
                        System.IO.Path.DirectorySeparatorChar
                    ),
                    !string.Equals(
                        element.Attribute("ReferenceOutputAssembly")?.Value,
                        "false",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            .Where(static dependency => dependency.Path.Length > 0)
            .ToArray();

        var targetFramework = document.Descendants("TargetFramework").FirstOrDefault()?.Value
            ?? InheritedTargetFramework(name);

        return new(name, path, targetFramework, packages, projects);
    }

    /// <summary>The profiles in Directory.Build.props set the framework by project name.</summary>
    static string InheritedTargetFramework(string name) =>
        name.EndsWith(".Rules", StringComparison.Ordinal) || name.EndsWith(".Generator", StringComparison.Ordinal)
            ? "netstandard2.0"
            : "net10.0";
}
