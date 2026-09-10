using System.Text.RegularExpressions;

namespace Rikarin.Skala.Core.Tests;

/// <summary>
///     The SK9000 range — the tool talking about itself — is allocated across more than one constant
///     class, and <c>rules.json</c>'s append-only guard does not cover it.
/// </summary>
/// <remarks>
///     ⚠ This test exists because the collision it forbids actually happened. The canonical
///     distribution work allocated <c>SK9010</c> and <c>SK9011</c>, both of which were already live in
///     the formatter as "file did not parse" and "unbalanced preprocessor structure"; it was caught by
///     eye during a merge, which is not a mechanism. ADR-012 makes an id permanent, and the reason is
///     baselines: a fingerprint carries the rule id, so one number with two meanings silently
///     un-suppresses one finding and wrongly suppresses the other in every repository holding one.
/// </remarks>
public sealed class ToolDiagnosticIdTests {
    static readonly Regex Literal = new("""  "SK\d{4}"  """.Trim(), RegexOptions.Compiled);

    /// <summary>A test may name an id freely — asserting on one is the point of a test.</summary>
    static bool IsTest(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}Testing{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || Path.GetFileName(Path.GetDirectoryName(path) ?? "").EndsWith(".Tests", StringComparison.Ordinal)
        || path.EndsWith("Tests.cs", StringComparison.Ordinal);

    static readonly Regex Declaration =
        new(
            """(?:public |internal |private )?const string (?<name>\w+)\s*=\s*"(?<id>SK\d{4})";""",
            RegexOptions.Compiled
        );

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

        // ⚠ One id may be declared twice when it is the *same* concept mirrored across an assembly
        // boundary: `Rikarin.Skala.Options.Generator` is netstandard2.0 with a restricted closure
        // (doc 02) and cannot reference Core's register, so it declares its own constant for a
        // diagnostic Core also names. That is a mirror, not a collision, and the test tells them
        // apart by concept rather than waving every duplicate through.
        var collisions = byId
            .Where(static entry => entry.Value.Select(Concept).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(static entry => $"{entry.Key} is declared as {string.Join(" and ", entry.Value)}")
            .ToList();

        Assert.True(
            collisions.Count == 0,
            "One id, two meanings. ADR-012 forbids it and a baseline cannot survive it — allocate "
            + "the next free number and add it to docs/plan/08's register:\n  "
            + string.Join("\n  ", collisions)
        );
    }

    /// <summary>
    ///     ⚠ Every <c>SK####</c> in product code must come from a named constant, never a bare literal.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the hole that let a real ADR-012 violation through after the other two assertions
    ///     were already in place. <c>SK9012</c> meant two things at once — the canonical-version
    ///     diagnostic and an I/O failure — and <c>SK9007</c> was in no register at all, because both
    ///     were written as bare literals at their call sites and
    ///     <see cref="ToolDiagnosticIds_AreDeclaredOnce" /> matches <c>public const string</c>. It read
    ///     declarations; the defects were uses. A guard that only sees the well-behaved half of the
    ///     codebase reports the codebase as well-behaved.
    /// </remarks>
    [Fact]
    public void ToolDiagnosticIds_AreNeverBareLiterals() {
        var offenders = new List<string>();

        foreach (var file in SourceFiles().Where(static path => !IsTest(path))) {
            var lineNumber = 0;
            foreach (var line in File.ReadAllLines(file)) {
                lineNumber++;
                var code = line.TrimStart();
                if (code.StartsWith("//", StringComparison.Ordinal)
                    || code.StartsWith('*')
                    || code.StartsWith("/*", StringComparison.Ordinal)) {
                    continue; // documentation naming an id is not an allocation
                }

                if (!Literal.IsMatch(line) || Declaration.IsMatch(line)) {
                    continue;
                }

                offenders.Add($"{Path.GetRelativePath(RepositoryRoot, file)}:{lineNumber}  {line.Trim()}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "An SK id written as a bare literal bypasses the register, and two of them collided "
            + "before this assertion existed. Declare it as a constant beside its siblings:\n  "
            + string.Join("\n  ", offenders)
        );
    }

    /// <summary>Every allocated id is in the register, so the register is not decoration.</summary>
    [Fact]
    public void ToolDiagnosticIds_AreInTheRegister() {
        var register = File.ReadAllText(Path.Combine(RepositoryRoot, "docs", "plan", "08-rule-catalogue.md"));

        var missing = SourceFiles()
            .SelectMany(static file => Declaration.Matches(File.ReadAllText(file))
                    .Select(static match => match.Groups["id"].Value)
            )
            .Where(static id => id.StartsWith("SK9", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Where(id => !register.Contains(id, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0, $"Not in docs/plan/08: {string.Join(", ", missing)}");
    }

    /// <summary>
    ///     ⚠ The other register. <c>allocated-ids.txt</c> is the file ADR-012 freezes, and until #352
    ///     nothing compared it against the ids the code can actually emit.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         Five live ids — <c>SK9015</c>, <c>SK9095</c>, <c>SK9096</c>, <c>SK9097</c> and
    ///         <c>SK9098</c> — were emitted by shipping code and were in neither <c>allocated-ids.txt</c>
    ///         nor <c>rules.json</c>, while their immediate siblings <c>SK9010</c>, <c>SK9011</c> and
    ///         <c>SK9099</c> were in both.
    ///     </b> So <c>skala explain SK9098</c> answered nothing, and the
    ///     SARIF notification for the diagnostic whose whole job is to say
    ///     <i>
    ///         "This is a Skala bug; the
    ///         file was left untouched"
    ///     </i> named a <c>rules[]</c> descriptor that was not there.
    ///     <para>
    ///         ⚠ <b>The guard that should have caught it could not see them.</b>
    ///         <c>RuleCatalogTests.ArrangementIds_AreUniqueRegisteredFormattingIds</c> reads exactly one
    ///         file, <c>Arrangement/ArrangementRule.cs</c>, and filters <c>SK9*</c> back out of it — so
    ///         <c>SK9097</c>, declared in <c>Arrangement/ArrangementPipeline.cs</c>, was outside its
    ///         input twice over. The other direction, <c>EveryCatalogueRule_IsRecordedAsAllocated</c>,
    ///         runs rules.json → register and can only ever find ids that are already in rules.json.
    ///         Nothing ran code → register, and a guard whose input is narrower than the set it claims
    ///         to cover returns the same green as a guard that is complete.
    ///     </para>
    ///     <para>
    ///         This one takes its input from <see cref="SourceFiles" />, which is the whole tree, so a
    ///         new id declared in a third place fails here rather than in a year's time. That is the
    ///         property under test, and <see cref="TheScan_ReadsTheTreeUnderTest" /> plus the
    ///         declaration-site assertion below are what stop it passing vacuously.
    ///     </para>
    /// </remarks>
    [Fact]
    public void ToolDiagnosticIds_AreAllocated() {
        var declared = DeclaredIds();
        var allocated = AllocatedIds();

        var missing = declared.Keys
            .Where(static id => !NotAllocated.Contains(id))
            .Where(id => !allocated.Contains(id))
            .Order(StringComparer.Ordinal)
            .Select(id => $"{id} declared at {string.Join(" and ", declared[id])}")
            .ToList();

        Assert.True(
            missing.Count == 0,
            "An id the code can emit is not in Rules/Rikarin.Skala.Rules.Metadata/allocated-ids.txt, "
            + "so the ledger does not describe reality and nothing stops the next allocation handing "
            + "the same number to something else — the exact hazard ADR-012 exists to prevent. Add a "
            + "`<id> <concept>` line there and a matching rules.json entry, so `skala explain` and the "
            + "SARIF `rules[]` descriptor exist:\n  "
            + string.Join("\n  ", missing)
        );
    }

    /// <summary>
    ///     ⚠ The exemption list above is debt, not licence, and this keeps it from becoming licence.
    /// </summary>
    /// <remarks>
    ///     Both directions are asserted. An entry whose id no longer exists in the tree is dead weight
    ///     that would silently re-authorise the number if it came back; an entry that <em>is</em> in
    ///     <c>allocated-ids.txt</c> is a line that has been paid off and must leave the list, or the
    ///     list would keep excusing an id whose absence it can no longer be excusing.
    /// </remarks>
    [Fact]
    public void TheUnallocatedToolDiagnostics_AreStillExactlyTheKnownDebt() {
        var declared = DeclaredIds();
        var allocated = AllocatedIds();

        var vanished = NotAllocated.Where(id => !declared.ContainsKey(id)).Order(StringComparer.Ordinal).ToList();
        Assert.True(
            vanished.Count == 0,
            "These ids are excused from allocation and are no longer declared anywhere. Drop them from "
            + "`NotAllocated` — an excuse nothing needs is an excuse waiting to cover something else:\n  "
            + string.Join("\n  ", vanished)
        );

        var paid = NotAllocated.Where(allocated.Contains).Order(StringComparer.Ordinal).ToList();
        Assert.True(
            paid.Count == 0,
            "These ids are excused from allocation and are now in allocated-ids.txt. Drop them from "
            + "`NotAllocated`, so the list is what is still owed and not what was once owed:\n  "
            + string.Join("\n  ", paid)
        );
    }

    /// <summary>
    ///     ⚠ Anti-vacuity for <see cref="ToolDiagnosticIds_AreAllocated" />, aimed at its actual failure
    ///     mode.
    /// </summary>
    /// <remarks>
    ///     The bug was never "the scan found nothing"; it was "the scan found one file". So it is not
    ///     enough to know the tree was read — the assertion has to be that ids from <em>every</em> place
    ///     that declares one reached it, named individually. <c>SK9097</c> is the load-bearing entry:
    ///     it is the id that lives in a second file inside the same folder as the one the old guard read.
    /// </remarks>
    [Fact]
    public void TheAllocationScan_ReadsEveryDeclarationSite() {
        var declared = DeclaredIds();

        foreach (var (id, site) in new[] {
                     ("SK9098", "ArrangementRule"), // ArrangeIds.Reverted
                     ("SK9096", "ArrangementRule"), // ArrangeIds.SymbolChanged
                     ("SK9095", "ArrangementRule"), // ArrangeIds.RuleThrew
                     ("SK9097", "ArrangementPipeline"), // ⚠ the site the old guard could not see
                     ("SK9015", "FormatDiagnosticIds"), // FormatDiagnosticIds.FileIoFailed
                     ("SK9099", "FormatDiagnosticIds"), ("SK9001", "SkalaDiagnostic"), // ConfigDiagnosticIds.UnknownKey
                     ("SK0201", "ArrangementRule")
                 }) {
            Assert.True(
                declared.TryGetValue(id, out var sites),
                $"{id} is declared in the tree and the allocation scan did not find it."
            );

            Assert.Contains(sites!, name => name.StartsWith(site + ".", StringComparison.Ordinal));
        }

        // Four distinct classes declare ids, and the whole defect was a guard that read one of them.
        Assert.True(
            declared.Values
                .SelectMany(static sites => sites)
                .Select(static site => site[..site.IndexOf('.', StringComparison.Ordinal)])
                .Distinct(StringComparer.Ordinal)
                .Count()
            >= 4,
            "The scan found fewer than four declaring types, which is fewer than the tree has."
        );
    }

    /// <summary>
    ///     Ids that are declared and deliberately absent from <c>allocated-ids.txt</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>SK3499</c> and <c>SK3500</c> are band edges in <c>RuleCoverage</c> — the boundaries of
    ///         the async and lifetime ranges, not rules. No rule may ever carry either, which is why
    ///         they are named rather than allocated.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>This list used to carry twenty-one more (#354)</b> — the configuration and load
    ///         diagnostics, <c>SK9002</c>–<c>SK9009</c>, <c>SK9012</c>–<c>SK9014</c>, <c>SK9016</c>,
    ///         <c>SK9017</c> and <c>SK9022</c>–<c>SK9029</c> — as debt enumerated rather than invisible:
    ///         each reached a SARIF <c>toolExecutionNotification</c> whose <c>descriptor.id</c> resolved
    ///         to nothing, and <c>skala explain</c> answered "not a Skala rule" for every one. All
    ///         twenty-one now have register lines and rules.json entries, and
    ///         <see cref="TheUnallocatedToolDiagnostics_AreStillExactlyTheKnownDebt" /> is what made them
    ///         leave this list on the same commit that paid them off. ⚠ The list stays frozen: it exempts
    ///         the two band edges and nothing else, so an id added in a third declaration site fails
    ///         <see cref="ToolDiagnosticIds_AreAllocated" /> on the commit that adds it.
    ///     </para>
    /// </remarks>
    static readonly HashSet<string> NotAllocated = new(StringComparer.Ordinal) { "SK3499", "SK3500" };

    /// <summary>Every id declared in the tree, mapped to the <c>Type.Member</c> sites declaring it.</summary>
    static Dictionary<string, List<string>> DeclaredIds() {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var file in SourceFiles()) {
            foreach (Match match in Declaration.Matches(File.ReadAllText(file))) {
                var id = match.Groups["id"].Value;
                if (!result.TryGetValue(id, out var sites)) {
                    result[id] = sites = [];
                }

                sites.Add($"{Path.GetFileNameWithoutExtension(file)}.{match.Groups["name"].Value}");
            }
        }

        Assert.NotEmpty(result);

        return result;
    }

    /// <summary>
    ///     ⚠ Read as text, not through <c>RuleCatalog</c>. The register is the artefact ADR-012 freezes,
    ///     and reading it through the generated catalogue would only ever prove rules.json agrees with
    ///     itself.
    /// </summary>
    static HashSet<string> AllocatedIds() {
        var path = Path.Combine(
            RepositoryRoot,
            "Rules",
            "Rikarin.Skala.Rules.Metadata",
            "allocated-ids.txt"
        );

        Assert.True(File.Exists(path), $"{path} does not exist; the register is what this test reads.");

        var result = File.ReadAllLines(path)
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0 && !line.StartsWith('#'))
            .Select(static line => line.Split(' ')[0])
            .ToHashSet(StringComparer.Ordinal);

        // Anti-vacuity: an unreadable or renamed register must not read as "nothing is allocated",
        // which would pass ToolDiagnosticIds_AreAllocated for every id at once.
        Assert.True(result.Count > 200, $"{path} was read and yielded only {result.Count} id(s).");
        Assert.Contains("SK9099", result);

        return result;
    }

    /// <summary>
    ///     Every hand-written source file under the tree being tested.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         The exclusions are matched against the path <i>relative to the root</i>, and before M7
    ///         they were matched against the absolute path. In a git worktree that made this whole class
    ///         pass vacuously.
    ///     </b> A worktree lives at
    ///     <c>&lt;repo&gt;/.claude/worktrees/&lt;name&gt;/</c>, so every absolute path inside one
    ///     contains <c>/worktrees/</c> — combine that with the root-finding bug below and the test
    ///     enumerated the <i>main checkout</i> and then excluded nothing, or enumerated the worktree
    ///     and excluded everything. Either way it was not reading the files under test, and it said so
    ///     by passing. Since the project is developed in worktrees, that is every run.
    ///     <para>
    ///         ADR-012 makes a rule id permanent at 1.0 because a baseline fingerprint carries it, so one
    ///         number with two meanings silently un-suppresses one finding and wrongly suppresses another
    ///         in every repository holding a baseline. A guard against that which does not read the diff is
    ///         worse than none, because it is believed.
    ///     </para>
    /// </remarks>
    /// <summary>The concept a constant names, ignoring its class and a trailing <c>Id</c>.</summary>
    static string Concept(string qualified) {
        var name = qualified[(qualified.LastIndexOf('.') + 1)..];
        return name.EndsWith("Id", StringComparison.Ordinal) ? name[..^2] : name;
    }

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
    ///     ⚠ Asserts that the scan found the tree it was supposed to find. Every other test in this
    ///     class is a "nothing is wrong" assertion, and those pass just as happily over an empty
    ///     sequence — which is exactly how the worktree bug above stayed invisible.
    /// </summary>
    [Fact]
    public void TheScan_ReadsTheTreeUnderTest() {
        var files = SourceFiles().ToList();

        Assert.True(files.Count > 100, $"Only {files.Count} source file(s) found under {RepositoryRoot}.");
        Assert.Contains(
            files,
            static path => path.EndsWith(
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
    ///     ⚠ A file <i>or</i> a directory. In a git worktree and in a submodule <c>.git</c> is a file
    ///     containing <c>gitdir: …</c>; testing only for a directory walks straight past the worktree's
    ///     own root and lands on the parent checkout, so the test then reads a different tree than the
    ///     one it was built from. This is the third place in the repository that had this exact bug.
    /// </summary>
    static bool IsRepository(string directory) {
        var marker = Path.Combine(directory, ".git");
        return Directory.Exists(marker) || File.Exists(marker);
    }
}
