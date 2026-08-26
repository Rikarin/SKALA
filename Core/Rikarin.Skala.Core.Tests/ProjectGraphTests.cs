using System.Xml.Linq;

namespace Rikarin.Skala.Core.Tests;

/// <summary>
/// The forbidden edges from docs/plan/02-repository-layout.md § "The project graph".
/// </summary>
/// <remarks>
/// ⚠ These are written against the projects that exist today and fail loudly when a project the
/// plan forbids referencing appears and is referenced. The plan is explicit that the graph is
/// "enforced by a test that walks the dependency closure, not by discipline" — a forbidden edge
/// added in Milestone 4 must break this test, not a user's analyzer load.
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
            foreach (var reference in project.ProjectReferences.Where(static r => r.Path.EndsWith("Rikarin.Skala.Cli.csproj", StringComparison.Ordinal))) {
                Assert.False(
                    reference.ReferencesOutputAssembly,
                    $"{project.Name} takes a compile-time reference on the CLI.");
            }
        }

        var sources = Directory.EnumerateFiles(RepositoryPaths.Root, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}Rikarin.Skala.Cli{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        foreach (var source in sources) {
            foreach (var line in File.ReadLines(source)) {
                Assert.NotEqual("using Rikarin.Skala.Cli;", line.Trim());
            }
        }
    }

    [Fact]
    public void FormattingKnowsNothingAboutCSharp() {
        // Unimplemented until Milestone 1; the guard is written now so that the first commit that
        // adds Rikarin.Skala.Formatting cannot quietly give it a Roslyn reference. The IR and the
        // fitting algorithm are what HTML and CSS reuse (docs/plan/14).
        var formatting = Projects.FirstOrDefault(static p => p.Name == "Rikarin.Skala.Formatting");
        if (formatting is null) {
            Assert.DoesNotContain(Projects, static p => p.Name.StartsWith("Rikarin.Skala.Formatting", StringComparison.Ordinal) && p.Name != "Rikarin.Skala.Formatting.CSharp");
            return;
        }

        Assert.DoesNotContain(formatting.PackageReferences, static package => package.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal));
        Assert.DoesNotContain(formatting.ProjectReferences, static reference => reference.Path.Contains("CSharp", StringComparison.Ordinal));
    }

    [Fact]
    public void TheAnalyzerPackageReferencesOnlyRoslynAndItsMetadata() {
        // Rikarin.Skala.Rules arrives in Milestone 5. Until then the guard covers the other project
        // on the analyzer profile, which has the same load constraints.
        foreach (var project in Projects.Where(static p => p.Name.EndsWith(".Rules", StringComparison.Ordinal) || p.Name.EndsWith(".Generator", StringComparison.Ordinal))) {
            Assert.Equal("netstandard2.0", project.TargetFramework);

            foreach (var package in project.PackageReferences) {
                Assert.True(
                    package.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal),
                    $"{project.Name} references '{package}'. An analyzer loads into csc and into Rider, and a transitive reference that is not netstandard2.0 fails the load with an error message that names none of it.");
            }

            foreach (var reference in project.ProjectReferences.Where(static r => r.ReferencesOutputAssembly)) {
                Assert.True(
                    reference.Path.EndsWith("Rules.Metadata.csproj", StringComparison.Ordinal),
                    $"{project.Name} references '{reference.Path}'.");
            }
        }
    }

    [Fact]
    public void CoreDoesNotReachIntoTheToolLayer() {
        var core = Assert.Single(Projects, static p => p.Name == "Rikarin.Skala.Core");
        Assert.All(core.ProjectReferences, reference => Assert.Contains("Rikarin.Skala.Options", reference.Path, StringComparison.Ordinal));
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
    public static IReadOnlyList<ProjectFile> LoadAll(string root) =>
        Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(Load)
            .OrderBy(static project => project.Name, StringComparer.Ordinal)
            .ToArray();

    static ProjectFile Load(string path) {
        var document = XDocument.Load(path);
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        var packages = document.Descendants("PackageReference")
            .Select(static element => element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value ?? string.Empty)
            .Where(static value => value.Length > 0 && !value.StartsWith("@(", StringComparison.Ordinal))
            .ToArray();
        var projects = document.Descendants("ProjectReference")
            .Select(static element => new ProjectDependency(
                (element.Attribute("Include")?.Value ?? string.Empty).Replace('\\', System.IO.Path.DirectorySeparatorChar),
                !string.Equals(element.Attribute("ReferenceOutputAssembly")?.Value, "false", StringComparison.OrdinalIgnoreCase)))
            .Where(static dependency => dependency.Path.Length > 0)
            .ToArray();

        var targetFramework = document.Descendants("TargetFramework").FirstOrDefault()?.Value
            ?? InheritedTargetFramework(name);

        return new ProjectFile(name, path, targetFramework, packages, projects);
    }

    /// <summary>The profiles in Directory.Build.props set the framework by project name.</summary>
    static string InheritedTargetFramework(string name) =>
        name.EndsWith(".Rules", StringComparison.Ordinal) || name.EndsWith(".Generator", StringComparison.Ordinal)
            ? "netstandard2.0"
            : "net10.0";
}
