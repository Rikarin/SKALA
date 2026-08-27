using System.Diagnostics;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
/// docs/plan/09 § "New-code definition" and § "Gates", against a real git repository.
/// </summary>
/// <remarks>
/// ⚠ A real repository rather than a fake, because every bug this code has had was in the
/// <em>interface</em> to git rather than in the logic around it: <c>git ls-tree -r -- "*.cs"</c>
/// matching nothing where <c>git ls-files "*.cs"</c> matches everything, and two-dot diff semantics
/// where three were meant. A mocked git cannot have either bug and so cannot catch either.
/// </remarks>
public sealed class NewCodeTests : IDisposable {
    readonly string _root = Path.Combine(Path.GetTempPath(), "skala-newcode-" + Path.GetRandomFileName());

    public NewCodeTests() {
        Directory.CreateDirectory(_root);
        Git("init", "-q");
        Git("config", "user.email", "test@skala");
        Git("config", "user.name", "Test");
    }

    public void Dispose() {
        try {
            Directory.Delete(_root, recursive: true);
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    void Git(params string[] arguments) {
        var start = new ProcessStartInfo("git") {
            WorkingDirectory = _root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments) {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
    }

    void Write(string relative, string content) {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    void Commit(string message) {
        Git("add", "-A");
        Git("commit", "-q", "-m", message);
    }

    Finding At(int line, string relative = "Core/Foo.cs") =>
        new() {
            RuleId = "SK1010",
            Severity = SkalaSeverity.Warning,
            Message = "x",
            Path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar)),
            Line = line,
            EndLine = line,
            Column = 1,
            EndColumn = 2
        };

    // ---------------------------------------------------------------- --since

    /// <summary>
    /// ⚠ Only the lines the ref actually changed, with no context.
    /// </summary>
    /// <remarks>
    /// <c>git diff</c> defaults to three lines of context either side. With them, a finding on an
    /// untouched line three above an edit counts as new code and a PR gate fails a branch for
    /// something it did not do — the fastest way to make people stop trusting <c>--since</c>.
    /// </remarks>
    [Fact]
    public void Since_MarksOnlyTheChangedLines() {
        Write("Core/Foo.cs", string.Join('\n', Enumerable.Range(1, 40).Select(i => "// line " + i)) + "\n");
        Commit("base");

        var lines = Enumerable.Range(1, 40).Select(i => "// line " + i).ToArray();
        lines[19] = "// CHANGED";
        Write("Core/Foo.cs", string.Join('\n', lines) + "\n");

        var changed = ChangedLines.Since(_root, "HEAD", TestContext.Current.CancellationToken);

        Assert.True(changed.Contains(At(20)));
        Assert.False(changed.Contains(At(17)));
        Assert.False(changed.Contains(At(23)));
        Assert.False(changed.Contains(At(1)));
    }

    [Fact]
    public void Since_MarksNothingWhenNothingChanged() {
        Write("Core/Foo.cs", "// one\n// two\n");
        Commit("base");

        var changed = ChangedLines.Since(_root, "HEAD", TestContext.Current.CancellationToken);
        Assert.Equal(0, changed.FileCount);
        Assert.False(changed.Contains(At(1)));
    }

    /// <summary>⚠ A pure deletion has no lines in the new file and must contribute no range.</summary>
    [Fact]
    public void Since_APureDeletionMarksNothingAtThePositionItLeftBehind() {
        Write("Core/Foo.cs", "// a\n// b\n// c\n// d\n// e\n");
        Commit("base");
        Write("Core/Foo.cs", "// a\n// e\n");

        var changed = ChangedLines.Since(_root, "HEAD", TestContext.Current.CancellationToken);

        // Everything that remains is unchanged text; nothing was added.
        Assert.False(changed.Contains(At(1)));
        Assert.False(changed.Contains(At(2)));
    }

    /// <summary>⚠ A ref git cannot resolve throws rather than reporting zero changed lines.</summary>
    /// <remarks>
    /// Zero changed lines would pass a <c>newIssues: 0</c> gate for the worst possible reason, and
    /// <c>--since=orgin/main</c> is a typo somebody will make.
    /// </remarks>
    [Fact]
    public void Since_AnUnresolvableRef_Throws() {
        Write("Core/Foo.cs", "// a\n");
        Commit("base");

        Assert.Throws<InvalidOperationException>(() => ChangedLines.Since(
                _root,
                "orgin/main",
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public void Since_TagsFindingsInPlace() {
        Write("Core/Foo.cs", string.Join('\n', Enumerable.Range(1, 10).Select(i => "// line " + i)) + "\n");
        Commit("base");

        var lines = Enumerable.Range(1, 10).Select(i => "// line " + i).ToArray();
        lines[4] = "// CHANGED";
        Write("Core/Foo.cs", string.Join('\n', lines) + "\n");

        var tagged = ChangedLines.Since(_root, "HEAD", TestContext.Current.CancellationToken).Apply([At(5), At(9)]);

        Assert.True(tagged[0].IsInChangedCode);
        Assert.False(tagged[1].IsInChangedCode);
    }

    /// <summary>
    /// ⚠ A file git has never seen is entirely new code.
    /// </summary>
    /// <remarks>
    /// <c>git diff</c> reports tracked files only. Without this, every finding in a file the branch
    /// <em>added</em> falls outside the changed ranges and a <c>newIssues: 0</c> gate passes on it —
    /// quiet in exactly the case the gate exists for.
    /// </remarks>
    [Fact]
    public void Since_AnUntrackedFileIsEntirelyNewCode() {
        Write("Core/Foo.cs", "// a\n");
        Commit("base");
        Write("Core/Added.cs", "// one\n// two\n// three\n");

        var changed = ChangedLines.Since(_root, "HEAD", TestContext.Current.CancellationToken);

        Assert.True(changed.Contains(At(1, "Core/Added.cs")));
        Assert.True(changed.Contains(At(3, "Core/Added.cs")));
        Assert.False(changed.Contains(At(1)));
    }

    /// <summary>⚠ An ignored file is build output, not somebody's new code.</summary>
    [Fact]
    public void Since_AnIgnoredFileIsNotNewCode() {
        Write(".gitignore", "generated/\n");
        Write("Core/Foo.cs", "// a\n");
        Commit("base");
        Write("generated/Gen.cs", "// generated\n");

        var changed = ChangedLines.Since(_root, "HEAD", TestContext.Current.CancellationToken);
        Assert.False(changed.Contains(At(1, "generated/Gen.cs")));
    }

    /// <summary>⚠ An uncommitted edit to a tracked file is new code too.</summary>
    /// <remarks>
    /// The bug this pins: <c>git diff ref...</c> with three dots means <c>ref...HEAD</c> and
    /// excludes the working tree entirely, so uncommitted work — most of what a developer runs this
    /// against — was invisible.
    /// </remarks>
    [Fact]
    public void Since_AnUncommittedEditToATrackedFileIsNewCode() {
        Write("Core/Foo.cs", "// a\n// b\n// c\n");
        Commit("base");
        Write("Core/Foo.cs", "// a\n// CHANGED\n// c\n");

        Assert.True(ChangedLines.Since(_root, "HEAD", TestContext.Current.CancellationToken).Contains(At(2)));
    }

    // ---------------------------------------------------------------- --no-new-suppressions

    /// <summary>
    /// ⚠ All four mechanisms, which is the whole requirement.
    /// </summary>
    /// <remarks>
    /// docs/plan/09: a grep for <c>#pragma</c> is not a constraint. This asserts the three that a
    /// grep would miss as well as the one it would find.
    /// </remarks>
    [Fact]
    public void Suppressions_DetectAPragmaAnAttributeAndAnEditorConfigDowngrade() {
        Write("Core/Foo.cs", "public class Foo { }\n");
        Write(".editorconfig", "[*.cs]\ndotnet_diagnostic.SK1010.severity = warning\n");
        Commit("base");

        Write(
            "Core/Foo.cs",
            "#pragma warning disable SK1010\n"
            + "using System.Diagnostics.CodeAnalysis;\n"
            + "[SuppressMessage(\"Skala.Async\", \"SK3002\")]\n"
            + "public class Foo { }\n"
        );

        Write(
            ".editorconfig",
            "[*.cs]\ndotnet_diagnostic.SK1010.severity = warning\n\n[Core/**/*.cs]\ndotnet_diagnostic.SK2015.severity = none\n"
        );

        var audit = SuppressionAuditor.Compare(
            _root,
            "HEAD",
            baselinePath: null,
            TestContext.Current.CancellationToken
        );

        Assert.True(audit.Enforced);
        Assert.Contains(audit.Added, static e => e is { Source: SuppressionSource.Pragma, RuleId: "SK1010" });
        Assert.Contains(audit.Added, static e => e is { Source: SuppressionSource.Attribute, RuleId: "SK3002" });
        Assert.Contains(
            audit.Added,
            static e => e is { Source: SuppressionSource.EditorConfig, RuleId: "SK2015", Detail: "none" }
        );
    }

    /// <summary>⚠ A baseline entry is a suppression, and the least visible of the four.</summary>
    [Fact]
    public void Suppressions_ABaselineEntryCountsAsOne() {
        Write("Core/Foo.cs", "public class Foo { }\n");
        Commit("base");

        var baselinePath = Path.Combine(_root, "baseline.sarif");
        var report = new RunReport {
            RepositoryRoot = _root, Mode = LoadMode.Loose, Findings = Fingerprints.Assign([At(3)])
        };

        Baseline.Write(baselinePath, report, report.Findings);

        var audit = SuppressionAuditor.Compare(_root, "HEAD", baselinePath, TestContext.Current.CancellationToken);
        Assert.Contains(audit.Added, static e => e is { Source: SuppressionSource.Baseline, RuleId: "SK1010" });
    }

    /// <summary>⚠ Nothing changed means nothing added — not "the ref looked empty".</summary>
    /// <remarks>
    /// The bug this pins: <c>git ls-tree -r -- "*.cs"</c> returns nothing where
    /// <c>git ls-files "*.cs"</c> returns everything, so the old side read as empty and every
    /// suppression in the repository was reported as newly added. Measured on a 2 705-file tree,
    /// that was 1 012 fabricated violations.
    /// </remarks>
    [Fact]
    public void Suppressions_AnUnchangedTreeAddsNothing() {
        Write("Core/Foo.cs", "#pragma warning disable SK1010\npublic class Foo { }\n");
        Write("Core/Deep/Nested/Bar.cs", "#pragma warning disable SK1030\npublic class Bar { }\n");
        Write(".editorconfig", "[*.cs]\ndotnet_diagnostic.SK2015.severity = none\n");
        Commit("base");

        var audit = SuppressionAuditor.Compare(
            _root,
            "HEAD",
            baselinePath: null,
            TestContext.Current.CancellationToken
        );

        Assert.Empty(audit.Added);
        Assert.Empty(audit.Removed);

        // ⚠ And the nested file was actually seen, so "empty" is not "found nothing at all".
        Assert.Contains(audit.Current, static e => e.RuleId == "SK1030");
        Assert.Contains(audit.Current, static e => e is { Source: SuppressionSource.EditorConfig, RuleId: "SK2015" });
    }

    /// <summary>⚠ A severity turned <em>up</em> is not a suppression.</summary>
    [Fact]
    public void Suppressions_ASeverityTurnedUpIsNotOne() {
        Write("Core/Foo.cs", "public class Foo { }\n");
        Write(".editorconfig", "[*.cs]\ndotnet_diagnostic.SK1010.severity = suggestion\n");
        Commit("base");

        Write(".editorconfig", "[*.cs]\ndotnet_diagnostic.SK1010.severity = error\n");

        var audit = SuppressionAuditor.Compare(
            _root,
            "HEAD",
            baselinePath: null,
            TestContext.Current.CancellationToken
        );
        Assert.DoesNotContain(audit.Added, static e => e.Source == SuppressionSource.EditorConfig);
    }

    /// <summary>
    /// ⚠ The section header is part of a suppression's identity.
    /// </summary>
    /// <remarks>
    /// Moving a severity line from a narrow section to a wide one changes nothing textually about
    /// the line and changes everything about what it silences.
    /// </remarks>
    [Fact]
    public void Suppressions_MovingASeverityToAWiderSectionIsANewSuppression() {
        Write("Core/Foo.cs", "public class Foo { }\n");
        Write(".editorconfig", "[Tools/**/*.cs]\ndotnet_diagnostic.SK3002.severity = none\n");
        Commit("base");

        Write(".editorconfig", "[**/*.cs]\ndotnet_diagnostic.SK3002.severity = none\n");

        var audit = SuppressionAuditor.Compare(
            _root,
            "HEAD",
            baselinePath: null,
            TestContext.Current.CancellationToken
        );
        Assert.Contains(audit.Added, static e => e is { Source: SuppressionSource.EditorConfig, RuleId: "SK3002" });
    }
}
