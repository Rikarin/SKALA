using System.Reflection;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>One corpus file the analyzer set is run over, named the way a failure has to read.</summary>
public sealed record CorpusCrashFile(string Set, string RelativePath, string Path) {
    public override string ToString() => Set + "/" + RelativePath;
}

/// <summary>
///     Every analyzer against <c>Testing/corpus/</c>, asking only whether one of them threw.
/// </summary>
/// <remarks>
///     ⚠
///     <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches an exception out of an
///     analyzer, reports it as <c>AD0001</c>, and drops that analyzer for the rest of the compilation —
///     so its positives fail and every "should not fire" case passes, which reads as a well-behaved
///     rule rather than as one that never ran. <c>skala check</c> cannot see it either: the crash is
///     renamed to <c>SK9030</c> and reaches only the SARIF's <c>toolExecutionNotifications</c>, never
///     <c>results</c> and never a gate verdict (#295).
///     <para>
///         ⚠
///         <b>
///             This suite exists because <c>RuleFixtureTests</c>' <c>AD0001</c> assertion covers
///             <c>fixtures/</c> and nothing else
///         </b>, and <c>fixtures/</c> is hand-written: every file in it
///         is a shape somebody thought of. <c>constructs/</c> and <c>pathological/</c> are 1 100-odd
///         files chosen to break a formatter, which makes them exactly the population a hand-written
///         fixture set cannot be. <c>SK7081</c> threw <c>IndexOutOfRangeException</c> on
///         <c>pathological/target-typed-new-of-a-delegate-with-a-query.cs</c> and no test in the
///         repository could see it (#315) — it was found by an agent sweeping the corpus by hand for
///         something else, which is not a check.
///     </para>
///     <para>
///         ⚠ <b>It deliberately does not assert the corpus compiles.</b> <c>RuleFixtureTests</c> does
///         that for a fixture, because a fixture that does not bind proves nothing about the rule it is
///         for. Here the opposite holds: the files that do not bind are the ones worth running, since a
///         crash inside Roslyn's own binder is what #315 turned out to be. A corpus file with errors is
///         the subject, not a broken test.
///     </para>
/// </remarks>
public sealed class CorpusCrashTests {
    /// <summary>
    ///     The two sets that are shapes rather than programs.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>real/</c> is left out on purpose and it is not an oversight. It is three vendored trees
    ///     of ordinary code, it is already swept by the parity and self-gate runs, and it is large
    ///     enough to dominate this suite's wall clock while being the least likely population to hold a
    ///     shape nobody has compiled before. The argument for adding it is real; it is a separate
    ///     measurement with its own budget.
    /// </remarks>
    static readonly string[] Sets = ["constructs", "pathological"];

    /// <summary>
    ///     ⚠ The floor that stops this suite passing by enumerating nothing.
    /// </summary>
    /// <remarks>
    ///     A glob that matches no file produces a theory with no cases, and xUnit reports a suite of
    ///     zero tests as a pass. That is the same "zero from a check that did not run" this whole file
    ///     exists to close, one level up — so the count is asserted as well as the crashes. The two
    ///     sets held 1 116 files when this was written; the floor is deliberately well below that
    ///     rather than equal to it, because adding and deleting corpus files is a legitimate commit
    ///     and pinning the exact number would make this suite a second, worse inventory of the corpus.
    /// </remarks>
    const int LeastPlausibleFileCount = 1000;

    static string RepositoryRoot { get; } = Assembly.GetExecutingAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(static attribute => attribute.Key == "SkalaRepositoryRoot")
        .Value!;

    public static TheoryData<CorpusCrashFile> Files {
        get {
            var data = new TheoryData<CorpusCrashFile>();
            foreach (var file in Enumerate()) {
                data.Add(file);
            }

            return data;
        }
    }

    /// <summary>
    ///     ⚠ <c>pathological/open/</c> is excluded, for the reason <c>Corpus.Files</c> excludes it.
    /// </summary>
    /// <remarks>
    ///     Those are minimised fuzz findings whose defect is <em>not fixed yet</em>, held to account by
    ///     <c>OpenDefectTests</c> instead. Including them here would assert that a known-open defect is
    ///     closed, and this suite would be red for a reason it is not about.
    ///     <para>
    ///         ⚠
    ///         <b>
    ///             The <c>.expected.cs</c> oracle fixtures are swept too, unlike everywhere else in
    ///             the repository
    ///         </b>, where they are the answer a measurement is compared against rather
    ///         than an input. Here they are just more C#: a fixture is <c>jb cleanupcode</c>'s
    ///         reformatting of the file beside it, which is a different token stream over the same
    ///         program, and trivia is precisely what <c>pathological/</c> is built to make hostile. It
    ///         doubles the swept population for about twelve seconds.
    ///     </para>
    ///     <para>
    ///         ⚠ This is also where #315's "1 117 files" comes from, and the number is worth pinning
    ///         because it reads as an input count and is not one. The two sets hold <b>504</b> inputs —
    ///         437 under <c>constructs/</c> and 67 under <c>pathological/</c> — beside 612 committed
    ///         oracle fixtures. 1 117 is the two added together, less the one open-defect file.
    ///     </para>
    /// </remarks>
    static IEnumerable<CorpusCrashFile> Enumerate() {
        var corpus = Path.Combine(RepositoryRoot, "Testing", "corpus");
        foreach (var set in Sets) {
            var root = Path.Combine(corpus, set);
            if (!Directory.Exists(root)) {
                continue;
            }

            var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .Where(static relative => !relative.StartsWith("open/", StringComparison.Ordinal))
                .OrderBy(static relative => relative, StringComparer.Ordinal);

            foreach (var relative in files) {
                yield return new CorpusCrashFile(set, relative, Path.Combine(root, relative));
            }
        }
    }

    [Fact]
    public void TheSweep_CoversTheWholeCorpus() {
        var found = Enumerate().Count();
        Assert.True(
            found >= LeastPlausibleFileCount,
            $"the corpus crash sweep enumerated {found} files, under the {LeastPlausibleFileCount} "
            + "that make it a sweep. An empty or truncated enumeration reports no crash for the same "
            + "reason a crashed analyzer reports no finding, so the count is asserted rather than "
            + "assumed."
        );
    }

    /// <summary>
    ///     ⚠ Every analyzer, not the one a file is about — a corpus file is about no rule.
    /// </summary>
    /// <remarks>
    ///     The set comes from <see cref="RuleFixtures.AllAnalyzers" /> rather than from a list here, so
    ///     that a newly shipped analyzer is enrolled in this sweep by the same edit that enrols it in
    ///     the fixture harness. A private second list is how a sweep goes quiet about exactly the
    ///     analyzer nobody remembered to add to it.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Files))]
    public void NoAnalyzer_ThrowsOnACorpusFile(CorpusCrashFile file) {
        var compilation = RuleFixtures.Compile(File.ReadAllText(file.Path), file.Path);

        var crashes = RuleFixtures
            .Analyze(compilation, RuleFixtures.AllAnalyzers, TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Id == "AD0001")
            .ToArray();

        Assert.True(
            crashes.Length == 0,
            $"{file}: an analyzer threw over this corpus file, so every rule it hosts silently "
            + "declined for the rest of the compilation and the run still reported success:\n  "
            + string.Join("\n  ", crashes.Select(static d => d.GetMessage()))
        );
    }
}
