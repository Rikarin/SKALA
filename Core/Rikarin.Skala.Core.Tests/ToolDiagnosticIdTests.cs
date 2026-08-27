using System.Text.RegularExpressions;

namespace Rikarin.Skala.Core.Tests;

/// <summary>
/// The SK9000 range — the tool talking about itself — is allocated across more than one constant
/// class, and <c>rules.json</c>'s append-only guard does not cover it.
/// </summary>
/// <remarks>
/// ⚠ This test exists because the collision it forbids actually happened. The canonical
/// distribution work allocated <c>SK9010</c> and <c>SK9011</c>, both of which were already live in
/// the formatter as "file did not parse" and "unbalanced preprocessor structure"; it was caught by
/// eye during a merge, which is not a mechanism. ADR-012 makes an id permanent, and the reason is
/// baselines: a fingerprint carries the rule id, so one number with two meanings silently
/// un-suppresses one finding and wrongly suppresses the other in every repository holding one.
/// </remarks>
public sealed class ToolDiagnosticIdTests {
    static readonly Regex Declaration =
        new("""public const string (?<name>\w+)\s*=\s*"(?<id>SK\d{4})";""", RegexOptions.Compiled);

    [Fact]
    public void ToolDiagnosticIds_AreDeclaredOnce() {
        var byId = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var file in SourceFiles()) {
            foreach (Match match in Declaration.Matches(File.ReadAllText(file))) {
                var id = match.Groups["id"].Value;
                var name = $"{Path.GetFileNameWithoutExtension(file)}.{match.Groups["name"].Value}";
                if (!byId.TryGetValue(id, out var names)) {
                    byId[id] = names = [];
                }

                names.Add(name);
            }
        }

        Assert.NotEmpty(byId);

        var collisions = byId
            .Where(entry => entry.Value.Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(entry => $"{entry.Key} is declared as {string.Join(" and ", entry.Value)}")
            .ToList();

        Assert.True(
            collisions.Count == 0,
            "One id, two meanings. ADR-012 forbids it and a baseline cannot survive it — allocate "
            + "the next free number and add it to docs/plan/08's register:\n  "
            + string.Join("\n  ", collisions)
        );
    }

    /// <summary>Every allocated id is in the register, so the register is not decoration.</summary>
    [Fact]
    public void ToolDiagnosticIds_AreInTheRegister() {
        var register = File.ReadAllText(Path.Combine(RepositoryRoot, "docs", "plan", "08-rule-catalogue.md"));

        var missing = SourceFiles()
            .SelectMany(file => Declaration.Matches(File.ReadAllText(file)).Select(match => match.Groups["id"].Value))
            .Where(id => id.StartsWith("SK9", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Where(id => !register.Contains(id, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0, $"Not in docs/plan/08: {string.Join(", ", missing)}");
    }

    static IEnumerable<string> SourceFiles() =>
        Directory
            .EnumerateFiles(RepositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal
                )
            )
            .Where(path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal
                )
            )
            .Where(path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}corpus{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal
                )
            )
            .Where(path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}fixtures{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal
                )
            )
            .Where(path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}worktrees{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal
                )
            );

    static string RepositoryRoot {
        get {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git"))) {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new InvalidOperationException("No repository root above the test binary.");
        }
    }
}
