using Rikarin.Skala.Testing;
using System.Diagnostics;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>The standalone arrangement command's project-loading contract.</summary>
public sealed class ArrangeCommandTests {
    [Fact]
    public void DefaultArrange_ClearsTheSemanticFindingReportedByDefaultVerify() {
        using var scratch = new Scratch();
        scratch.Write(
            ".editorconfig",
            """
            root = true

            [*.cs]
            skala_arguments_literal = named
            skala_arguments_skip_single = false
            """
        );
        scratch.Write(
            "Callee.cs",
            """
            internal static class Callee {
                public static int Sum(int first, int second) => first + second;
            }
            """
        );
        var caller = scratch.Write(
            "Caller.cs",
            """
            internal static class Caller {
                public static int Call() => Callee.Sum(1, 2);
            }
            """
        );
        scratch.Write(
            "Scratch.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """
        );

        var arranged = CliRunner.Run("arrange", caller);

        Assert.Equal(0, arranged.ExitCode);
        Assert.Contains("Callee.Sum(first: 1, second: 2)", File.ReadAllText(caller), StringComparison.Ordinal);

        var verified = CliRunner.Run("verify", caller, "--no-cache");

        Assert.DoesNotContain("SK0216", verified.StandardOutput, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A solution nested below the Git root is still the workspace target when <c>arrange</c>
    ///     is run from that project without an explicit path.
    /// </summary>
    /// <remarks>
    ///     ⚠ The collected files are below <c>App/src</c>, while the solution is at <c>App/App.slnx</c>.
    ///     Re-deriving discovery from the first collected file searches <c>src</c> and then the outer
    ///     Git root, skipping <c>App</c>; the loose compilation then lacks the project-defined symbol
    ///     and names the arguments after the wrong conditional signature.
    /// </remarks>
    [Fact]
    public void DefaultArrange_LoadsTheSlnxUnderTheRequestedNestedProject() {
        using var scratch = new Scratch();
        scratch.Write(
            "App/.editorconfig",
            """
            root = true

            [*.cs]
            skala_arguments_literal = named
            skala_arguments_skip_single = false
            """
        );
        scratch.Write(
            "App/src/Callee.cs",
            """
            internal static class Callee {
            #if PROJECT_BUILD
                public static int Sum(int first, int second) => first + second;
            #else
                public static int Sum(int x, int y) => x + y;
            #endif
            }
            """
        );
        var caller = scratch.Write(
            "App/src/Caller.cs",
            """
            internal static class Caller {
                public static int Call() => Callee.Sum(1, 2);
            }
            """
        );
        scratch.Write(
            "App/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <DefineConstants>PROJECT_BUILD</DefineConstants>
              </PropertyGroup>
            </Project>
            """
        );
        scratch.Write(
            "App/App.slnx",
            """
            <Solution>
              <Project Path="App.csproj" />
            </Solution>
            """
        );

        var arranged = RunIn(Path.Combine(scratch.Root, "App"), "arrange");

        Assert.Equal(0, arranged.ExitCode);
        var rewritten = File.ReadAllText(caller);
        Assert.Contains("Callee.Sum(first: 1, second: 2)", rewritten, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ #336: <c>arrange</c> had neither <c>--binlog</c> nor <c>--require-fresh-binlog</c>, so
    ///     <c>--load=binlog</c> could only auto-discover and could never be told the log had to be
    ///     current.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The named binlog is what makes this test able to fail.</b> Without <c>--binlog</c>
    ///     there is nothing to point at a log that does not exist, so the absence of the option is
    ///     also the absence of any way to demonstrate the absence — which is why it went unnoticed.
    ///     <c>--require-fresh-binlog</c> then has to reach <c>LoadRequest</c> rather than merely parse:
    ///     sabotage by dropping <c>RequireFreshBinlog</c> from <c>CompilationsFor</c>'s
    ///     <c>LoadRequest</c> and the second half goes red while the first still passes.
    /// </remarks>
    [Fact]
    public void Arrange_TakesTheBinlogOptionsCheckHas() {
        using var scratch = new Scratch();
        var file = scratch.Write("Widget.cs", "internal sealed class Widget;");

        // Exists, so binlog resolution selects it, and is not a binary log, so reading it fails
        // naming the path. That naming is the assertion: it can only come from --binlog reaching
        // LoadRequest.BinlogPath, since nothing else in the run knows this file.
        var named = scratch.Write("named.binlog", "not a binary log");

        var withBinlog = CliRunner.Run("arrange", "--check", "--load=binlog", "--binlog", named, file);

        Assert.DoesNotContain("Unrecognized command or argument", withBinlog.StandardError, StringComparison.Ordinal);
        Assert.Contains(named, withBinlog.StandardOutput + withBinlog.StandardError, StringComparison.Ordinal);

        var withFresh = CliRunner.Run(
            "arrange",
            "--check",
            "--load=binlog",
            "--binlog",
            named,
            "--require-fresh-binlog",
            file
        );

        Assert.DoesNotContain("Unrecognized command or argument", withFresh.StandardError, StringComparison.Ordinal);

        // ⚠ **Stated gap, not an oversight.** `--require-fresh-binlog` only changes the severity of
        // the coverage and staleness diagnostics, both of which need a *readable* binary log, and
        // this repository has no binlog fixture and no cheap way to make one — producing it means a
        // real `dotnet build -bl:`. So the option's presence is asserted here and its effect is
        // exercised by `build/Build.cs`'s Lint target, which passes it on every run. A test that
        // pretended to cover the effect would be worth less than saying so.
        Assert.Equal(withBinlog.ExitCode, withFresh.ExitCode);
    }

    /// <summary>
    ///     ⚠ #381, end to end: an unresolvable using survives <c>arrange</c> under both loaders, and an
    ///     unused resolvable one beside it still goes.
    /// </summary>
    /// <remarks>
    ///     Measured before the fix, both loaders deleted all four broken directives along with
    ///     <c>System.Text</c>, because Roslyn reports <c>CS8019</c> for a directive that does not resolve.
    ///     The two loaders are asserted separately because they reach <c>UsingsRule.Unused</c> through
    ///     different compilations — the loose loader's binds the requested files against the running
    ///     shared framework, the workspace's against the project's own references — and #381 measured the
    ///     defect in each.
    ///     <para>
    ///         ⚠ #395 changed the loose half: a loose load now arranges the syntactic subset under every
    ///         verb, so it removes <em>nothing</em> — <c>System.Text</c> included — and the broken
    ///         directives survive because nothing is removed at all. The unresolvable-directive guard is
    ///         therefore exercised by the workspace row only; the loose row pins that a loose
    ///         <c>arrange</c> agrees with a loose <c>verify</c>.
    ///     </para>
    /// </remarks>
    [Theory]
    [InlineData("loose", false)]
    [InlineData("workspace", true)]
    public void Arrange_KeepsAnUnresolvableUsing_AndRemovesAnUnusedResolvableOne(string mode, bool removes) {
        using var scratch = new Scratch();
        var file = scratch.Write(
            "Probe.cs",
            """
            using System;
            using System.Text;
            using Xyz.Alpha;
            using System.DoesNotExist;
            using A = Missing.Type;
            using static Missing.Statics;

            namespace P;

            public class Probe {
                public void M() => Console.WriteLine();
            }
            """
        );
        scratch.Write(
            "Scratch.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>disable</ImplicitUsings>
              </PropertyGroup>
            </Project>
            """
        );

        var arranged = RunIn(scratch.Root, "arrange", "--load=" + mode, file);

        Assert.Equal(0, arranged.ExitCode);
        var rewritten = File.ReadAllText(file);
        Assert.Equal(!removes, rewritten.Contains("using System.Text;", StringComparison.Ordinal));
        Assert.Contains("using System;", rewritten, StringComparison.Ordinal);
        Assert.Contains("using Xyz.Alpha;", rewritten, StringComparison.Ordinal);
        Assert.Contains("using System.DoesNotExist;", rewritten, StringComparison.Ordinal);
        Assert.Contains("using A = Missing.Type;", rewritten, StringComparison.Ordinal);
        Assert.Contains("using static Missing.Statics;", rewritten, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ #395: <c>verify</c> is <c>arrange --check</c> plus two other stages, under every load mode — the
    ///     same files reported, with the same rules, by both verbs.
    /// </summary>
    /// <remarks>
    ///     One file per semantic arrangement rule, plus the issue's own <c>F.cs</c> (an unused
    ///     <c>using System.Text;</c>, already sorted). Before the fix a loose <c>verify</c> was green while a
    ///     loose <c>arrange --check</c> exited 2 on all twelve, because the two verbs decided separately
    ///     whether a loose compilation counts. Both now ask <c>ArrangementCompilations</c>.
    ///     <para>
    ///         ⚠ The workspace row is the anti-vacuity half: every shape must be reported there, by both
    ///         verbs, or "both verbs agree on nothing" would pass the loose row over a broken harness.
    ///     </para>
    /// </remarks>
    [Theory]
    [InlineData("loose")]
    [InlineData("workspace")]
    public void Verify_ReportsExactlyTheFilesArrangeCheckWouldArrange(string mode) {
        using var scratch = new Scratch();
        scratch.Write(
            ".editorconfig",
            """
            root = true

            [*.cs]
            skala_empty_string = empty_literal
            """
        );
        scratch.Write(
            "Scratch.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>disable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """
        );
        scratch.Write(
            "Types.cs",
            """
            namespace P;

            public sealed class Thing {
                public int Count { get; set; }

                public static Thing Create() => new();
            }

            public sealed class Flags {
                public bool A { get; init; }

                public bool B { get; init; }
            }
            """
        );

        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, rule, source) in SemanticShapes) {
            scratch.Write(name, source);
            expected[name] = rule;
        }

        var arranged = RunIn(scratch.Root, "arrange", "--check", "--load=" + mode, scratch.Root);
        var verified = RunIn(scratch.Root, "verify", "--load=" + mode, "--no-cache", "--format", "plain", scratch.Root);

        var byArrange = ArrangedBy(arranged.StandardOutput);
        var byVerify = ReportedBy(verified.StandardOutput);

        // The invariant itself, before either side's expectation: the same files, the same rules.
        Assert.Equal(Render(byArrange), Render(byVerify));
        Assert.Equal(byArrange.Count > 0 ? 2 : 0, arranged.ExitCode);

        if (mode == "workspace") {
            Assert.Equal(
                Render(expected.ToDictionary(static pair => pair.Key, static pair => new[] { pair.Value })),
                Render(byArrange)
            );
        } else {
            Assert.Empty(byArrange);
            Assert.Equal(0, verified.ExitCode);
        }
    }

    /// <summary>One shape per semantic arrangement rule; each file is otherwise formatted and arranged.</summary>
    static readonly (string File, string Rule, string Source)[] SemanticShapes = [
        ("F.cs", "usings", """
                           using System;
                           using System.Text;

                           namespace P;

                           public class D {
                               public void M() => Console.WriteLine();
                           }
                           """),
        ("Var.cs", "var", """
                          namespace P;

                          public static class Var {
                              public static int M() {
                                  Thing t = Thing.Create();
                                  return t.Count;
                              }
                          }
                          """),
        ("ObjectCreation.cs", "target-typed new", """
                                                  namespace P;

                                                  public sealed class ObjectCreation {
                                                      readonly Thing thing = new Thing();

                                                      public int M() => thing.Count;
                                                  }
                                                  """),
        ("DefaultValue.cs", "default literal", """
                                               using System.Threading;

                                               namespace P;

                                               public static class DefaultValue {
                                                   public static int M(CancellationToken c = default(CancellationToken)) => 0;
                                               }
                                               """),
        ("NullChecking.cs", "is not null", """
                                           namespace P;

                                           public static class NullChecking {
                                               public static bool M(object o) => o == null;
                                           }
                                           """),
        ("EmptyString.cs", "empty string literal", """
                                                   namespace P;

                                                   public static class EmptyString {
                                                       public static string M() => string.Empty;
                                                   }
                                                   """),
        ("ThisQualifier.cs", "this qualifier", """
                                               namespace P;

                                               public sealed class ThisQualifier {
                                                   readonly int count = 1;

                                                   public int M() => this.count;
                                               }
                                               """),
        ("Predefined.cs", "predefined type", """
                                             namespace P;

                                             public static class Predefined {
                                                 public static int M(System.String s) => s.Length;
                                             }
                                             """),
        ("StaticQualifier.cs", "static member qualifier", """
                                                          namespace P;

                                                          public static class StaticQualifier {
                                                              static int Helper() => 1;

                                                              public static int M() => StaticQualifier.Helper();
                                                          }
                                                          """),
        ("ArgumentStyle.cs", "argument style", """
                                               namespace P;

                                               public static class ArgumentStyle {
                                                   static int Take(int value, bool flag) => flag ? value : 0;

                                                   public static int M() => Take(value: 1, flag: true);
                                               }
                                               """),
        ("PropertyPattern.cs", "property pattern", """
                                                   namespace P;

                                                   public static class PropertyPattern {
                                                       public static bool M(Flags f) => f.A && !f.B;
                                                   }
                                                   """)
    ];

    /// <summary><c>arrange --check</c>'s per-file lines: <c>path␣␣rule name, rule name</c>.</summary>
    static Dictionary<string, string[]> ArrangedBy(string output) {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var line in output.Split('\n')) {
            var separator = line.IndexOf("  ", StringComparison.Ordinal);
            if (separator <= 0 || !line[..separator].EndsWith(".cs", StringComparison.Ordinal)) {
                continue;
            }

            result[Path.GetFileName(line[..separator])] = Names(line[(separator + 2)..]);
        }

        return result;
    }

    /// <summary><c>verify --format plain</c>'s arrangement findings: <c>path:l:c: … not arranged (names); run: …</c>.</summary>
    static Dictionary<string, string[]> ReportedBy(string output) {
        const string marker = "the file is not arranged (";
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var line in output.Split('\n')) {
            var at = line.IndexOf(marker, StringComparison.Ordinal);
            if (at < 0) {
                continue;
            }

            var names = line[(at + marker.Length)..];
            result[Path.GetFileName(line[..line.IndexOf(':', StringComparison.Ordinal)])] =
                Names(names[..names.IndexOf(')', StringComparison.Ordinal)]);
        }

        return result;
    }

    static string[] Names(string list) =>
        [.. list.Trim().Split(", ").Order(StringComparer.Ordinal)];

    static string Render(IReadOnlyDictionary<string, string[]> map) =>
        string.Join(
            "\n",
            map.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => pair.Key + ": " + string.Join(", ", pair.Value))
        );

    static CliRun RunIn(string workingDirectory, params string[] arguments) {
        var start = new ProcessStartInfo("dotnet") {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        start.ArgumentList.Add(CliRunner.Assembly);
        foreach (var argument in arguments) {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new(process.ExitCode, output, error);
    }

    sealed class Scratch : IDisposable {
        public Scratch() {
            Root = Path.Combine(Path.GetTempPath(), "skala-cli-arrange", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(Root);

            // Repository discovery is intentionally part of the test: auto-load must resolve the
            // project beside the requested file, not a solution beside the test runner.
            Directory.CreateDirectory(Path.Combine(Root, ".git"));
        }

        public string Root { get; }

        public string Write(string relative, string text) {
            var path = Path.Combine(Root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
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
