using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>The fix command discovers the same workspace as arrange and verify.</summary>
public sealed class FixCommandTests {
    const string IncludeOption = "--include";

    const string Project = """
                           <Project Sdk="Microsoft.NET.Sdk">
                             <PropertyGroup>
                               <TargetFramework>net10.0</TargetFramework>
                             </PropertyGroup>
                           </Project>
                           """;

    const string Source = """
                          public static class Factory {
                              public static System.Func<int, int> Create() => value => value + 1;
                          }
                          """;

    /// <summary>
    ///     The reproduction from #342, which is the reproduction from #344.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>SK0231</c> reports the second operand as an interpolated string with no holes and offers
    ///     to drop the <c>$</c>, and the fix is marked safe. It is also the edit that makes the whole
    ///     additive expression a <c>string</c> rather than a <c>DefaultInterpolatedStringHandler</c>, so
    ///     the <c>string.Create</c> overload stops applying and the call binds to a <c>ref</c> parameter
    ///     it cannot bind to. <b>Nothing about that is syntactic</b> — the file parses perfectly either
    ///     way — which is why the shipping parse-only check let it through and exited 0.
    /// </remarks>
    /// <summary>
    ///     A <c>const</c> that is read from a constant position, so SK6034's <c>static readonly</c>
    ///     rewrite parses and does not bind.
    /// </summary>
    /// <remarks>
    ///     ⚠ This fixture used to be #342's <c>string.Create</c> shape, and that was a mistake worth
    ///     recording: it rested on SK0231 firing where it should not, so fixing SK0231 turned this
    ///     test's premise into "nothing to apply" and the test went red on the merged tree rather
    ///     than on either branch.
    ///     <b>
    ///         A regression test for the safety net must not be built on a
    ///         rule's false positive
    ///     </b> — the net outlives the bug. SK6034 is a true positive whose
    ///     rewrite is genuinely illegal here, so nothing about this fixture depends on a defect.
    /// </remarks>
    const string ConstantSource = """
                                  namespace Probe;

                                  public static class Limits {
                                      public const int Max = 8;

                                      public static bool Over(int n) =>
                                          n switch {
                                              Max => true,
                                              _ => false
                                          };
                                  }
                                  """;

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Fix_AutoAppliesSemanticFixes(bool explicitAuto, bool safe) {
        using var scratch = new Scratch();
        var source = scratch.Write("Factory.cs", Source);
        scratch.Write("Scratch.csproj", Project);
        var arguments = new List<string> { "fix", source };
        if (explicitAuto) {
            arguments.Add("--load=auto");
        }

        if (safe) {
            arguments.Add("--safe");
        } else {
            arguments.AddRange([IncludeOption, "SK4020"]);
        }

        var run = CliRunner.Run([.. arguments]);

        Assert.True(run.ExitCode == 0, run.StandardOutput + run.StandardError);
        Assert.Contains("static value => value + 1", File.ReadAllText(source), StringComparison.Ordinal);
    }

    [Fact]
    public void Fix_ProjectSelectsOneOfSeveralTargetsAndDryRunDoesNotWrite() {
        using var scratch = new Scratch();
        var source = scratch.Write("Factory.cs", Source);
        var project = scratch.Write("First.csproj", Project);
        scratch.Write("Second.csproj", Project);
        var before = File.ReadAllText(source);

        var run = CliRunner.Run("fix", source, IncludeOption, "SK4020", "--project", project, "--dry-run");

        Assert.True(run.ExitCode == 0, run.StandardOutput + run.StandardError);
        Assert.Contains("applied 1 fix (dry run, nothing written)", run.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(source));
    }

    [Fact]
    public void Fix_AutoRefusesAmbiguousTargets() {
        using var scratch = new Scratch();
        var source = scratch.Write("Factory.cs", Source);
        scratch.Write("First.csproj", Project);
        scratch.Write("Second.csproj", Project);
        var before = File.ReadAllText(source);

        var run = CliRunner.Run("fix", source, IncludeOption, "SK4020");

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains("multiple '*.csproj' workspace targets", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--project", run.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(source));
    }

    [Fact]
    public void Fix_AutoRefusesAFailedWorkspaceLoad() {
        using var scratch = new Scratch();
        var source = scratch.Write("Factory.cs", Source);
        scratch.Write("Scratch.csproj", "<Project>");
        var before = File.ReadAllText(source);

        var run = CliRunner.Run("fix", source, IncludeOption, "SK4020");

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains("no compilation could be built", run.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(source));
    }

    [Fact]
    public void Fix_ExplicitLooseDoesNotLoadTheWorkspace() {
        using var scratch = new Scratch();
        var source = scratch.Write("Factory.cs", Source);
        scratch.Write("Scratch.csproj", "<Project>");
        var before = File.ReadAllText(source);

        var run = CliRunner.Run("fix", source, IncludeOption, "SK4020", "--load=loose");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("nothing to apply", run.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(source));
    }

    /// <summary>
    ///     ⚠ The regression test for #344: a fix that parses and does not bind must be reverted.
    /// </summary>
    /// <remarks>
    ///     ⚠ Sabotage check — point <c>FixSafety.Verify</c> back at the parse-only path (return the
    ///     <c>Syntactic</c> branch unconditionally) and this test must go red. It did not before the
    ///     fix: the shipping <c>Diagnostics(string)</c> was <c>CSharpSyntaxTree.ParseText</c>, whose
    ///     <c>GetDiagnostics</c> is syntactic only, so the rewritten file's error count was identical
    ///     and the guard passed. Measured against a stale Release binary during the merge, which
    ///     applied the same edit and left the file broken — the two builds disagreeing on one input
    ///     is the cleanest statement of what this test holds down.
    ///     <para>
    ///         ⚠ <c>--include</c> rather than <c>--safe</c>: SK6034's fix is <c>fixIsSafe: false</c>,
    ///         and the revert path this covers is the same one either way. What matters is that the
    ///         rewrite is a true positive whose result does not bind, not which bucket it ships in.
    ///     </para>
    /// </remarks>
    [Fact]
    public void Fix_RevertsAFixThatStopsBinding() {
        using var scratch = new Scratch();
        var source = scratch.Write("Limits.cs", ConstantSource);
        scratch.Write("Scratch.csproj", Project);
        var before = File.ReadAllText(source);

        var run = CliRunner.Run("fix", scratch.Root, IncludeOption, "SK6034");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("Limits.cs was reverted", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("CS9135", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("reverted 1 file(s) that regressed", run.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(source));
    }

    /// <summary>The positive control: a safe fix that still binds is still written.</summary>
    [Fact]
    public void Fix_AppliesASafeFixThatStillBinds() {
        using var scratch = new Scratch();
        var source = scratch.Write("Factory.cs", Source);
        scratch.Write("Scratch.csproj", Project);

        var run = CliRunner.Run("fix", scratch.Root, "--safe");

        Assert.True(run.ExitCode == 0, run.StandardOutput + run.StandardError);
        Assert.DoesNotContain("was reverted", run.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("parse errors only", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("static value => value + 1", File.ReadAllText(source), StringComparison.Ordinal);
    }

    [Fact]
    public void Fix_WithoutAWorkspaceStillAppliesSyntacticFixes() {
        using var scratch = new Scratch();
        var source = scratch.Write(
            "Thrower.cs",
            """
            public static class Thrower {
                public static void Run() {
                    try { System.Console.WriteLine(); } catch (System.Exception ex) { throw ex; }
                }
            }
            """
        );

        var run = CliRunner.Run("fix", source, IncludeOption, "SK2015");

        Assert.True(run.ExitCode == 0, run.StandardOutput + run.StandardError);
        Assert.Contains("throw;", File.ReadAllText(source), StringComparison.Ordinal);

        // ⚠ #344: with no project there is no compilation to re-bind in, so this fix got the parse
        // check and nothing else. That is defensible and it is said out loud — what is not defensible
        // is letting it pass for the check the other files got.
        Assert.Contains("checked for parse errors only", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("a build is still owed", run.StandardOutput, StringComparison.Ordinal);
    }

    sealed class Scratch : IDisposable {
        public Scratch() {
            Root = Path.Combine(Path.GetTempPath(), "skala-cli-fix", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(Path.Combine(Root, ".git"));
            Write(".editorconfig", "root = true");
        }

        public string Root { get; }

        public string Write(string relative, string text) {
            var path = Path.Combine(Root, relative);
            File.WriteAllText(path, text + "\n");
            return path;
        }

        public void Dispose() {
            try {
                Directory.Delete(Root, true);
            } catch (IOException) {
                // A leftover temp directory is not worth failing a test over.
            }
        }
    }
}
