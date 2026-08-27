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

    /// <summary>
    /// Every hand-written source file under the tree being tested.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>The exclusions are matched against the path <i>relative to the root</i>, and before M7
    /// they were matched against the absolute path. In a git worktree that made this whole class
    /// pass vacuously.</b> A worktree lives at
    /// <c>&lt;repo&gt;/.claude/worktrees/&lt;name&gt;/</c>, so every absolute path inside one
    /// contains <c>/worktrees/</c> — combine that with the root-finding bug below and the test
    /// enumerated the <i>main checkout</i> and then excluded nothing, or enumerated the worktree
    /// and excluded everything. Either way it was not reading the files under test, and it said so
    /// by passing. Since the project is developed in worktrees, that is every run.
    /// <para>
    /// ADR-012 makes a rule id permanent at 1.0 because a baseline fingerprint carries it, so one
    /// number with two meanings silently un-suppresses one finding and wrongly suppresses another
    /// in every repository holding a baseline. A guard against that which does not read the diff is
    /// worse than none, because it is believed.
    /// </para>
    /// </remarks>
    static IEnumerable<string> SourceFiles() {
        var root = RepositoryRoot;
        var separator = Path.DirectorySeparatorChar;
        string[] excluded = ["obj", "bin", "corpus", "fixtures", ".claude", "artifacts"];

        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)) {
            var relative = separator + Path.GetRelativePath(root, path) + separator;
            var skip = false;
            foreach (var segment in excluded) {
                if (relative.Contains(separator + segment + separator, StringComparison.Ordinal)) {
                    skip = true;
                    break;
                }
            }

            if (!skip) {
                yield return path;
            }
        }
    }

    /// <summary>
    /// ⚠ Asserts that the scan found the tree it was supposed to find. Every other test in this
    /// class is a "nothing is wrong" assertion, and those pass just as happily over an empty
    /// sequence — which is exactly how the worktree bug above stayed invisible.
    /// </summary>
    [Fact]
    public void TheScan_ReadsTheTreeUnderTest() {
        var files = SourceFiles().ToList();

        Assert.True(files.Count > 100, $"Only {files.Count} source file(s) found under {RepositoryRoot}.");
        Assert.Contains(
            files,
            path => path.EndsWith(
                Path.Combine("Rikarin.Skala.Core.Tests", "ToolDiagnosticIdTests.cs"),
                StringComparison.Ordinal
            )
        );
    }

    static string RepositoryRoot {
        get {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !IsRepository(directory.FullName)) {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new InvalidOperationException("No repository root above the test binary.");
        }
    }

    /// <summary>
    /// ⚠ A file <i>or</i> a directory. In a git worktree and in a submodule <c>.git</c> is a file
    /// containing <c>gitdir: …</c>; testing only for a directory walks straight past the worktree's
    /// own root and lands on the parent checkout, so the test then reads a different tree than the
    /// one it was built from. This is the third place in the repository that had this exact bug.
    /// </summary>
    static bool IsRepository(string directory) {
        var marker = Path.Combine(directory, ".git");
        return Directory.Exists(marker) || File.Exists(marker);
    }
}
