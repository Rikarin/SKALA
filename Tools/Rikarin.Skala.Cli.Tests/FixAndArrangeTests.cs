using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>
///     <c>skala fix</c> and <c>skala arrange</c> reach a fixed point together, at every value of the key
///     that decides whether they can disagree.
/// </summary>
/// <remarks>
///     ⚠ #394. <c>SK2174</c>'s fix parenthesises a binary operand of a shift or bitwise operator, and
///     <c>SK0209</c> removes redundant parentheses. At the export's
///     <c>remove_if_not_clarifies_precedence</c> the arranger keeps exactly those, and at <c>remove</c> it
///     strips them as the oracle does — so with <c>SK2174</c> blind to the key, <c>fix</c> and
///     <c>arrange</c> undid each other on every run while three texts said the boundary was "settled by
///     construction". The claim is now the assertion: fix, arrange, fix, arrange, and nothing moves
///     after the first round.
/// </remarks>
public sealed class FixAndArrangeTests {
    const string Source = """
                          namespace P;

                          public static class Bits {
                              public static int Or(int a, int b, int c) => a | b & c;

                              public static int Shift(int value, int offset) => value << offset + 1;

                              public static int Mask(int mask, int offset) => mask & offset + 1;

                              public static int Kept(int a, int b, int c) => a | (b & c);
                          }
                          """;

    [Theory]
    [InlineData(null)]
    [InlineData("remove_if_not_clarifies_precedence")]
    [InlineData("remove")]
    public void FixThenArrange_IsAFixedPoint_AtEveryValueOfTheRedundancyStyle(string? style) {
        using var scratch = new Scratch();
        scratch.Write(
            ".editorconfig",
            style is null
                ? "root = true\n\n[*.cs]\nindent_style = space\n"
                : $"root = true\n\n[*.cs]\nindent_style = space\nskala_parentheses_redundancy_style = {style}\n"
        );
        var file = scratch.Write("Bits.cs", Source);

        Fix(file);
        var fixedOnce = File.ReadAllText(file);
        Arrange(file);
        var arrangedOnce = File.ReadAllText(file);

        Fix(file);
        Assert.Equal(arrangedOnce, File.ReadAllText(file));
        Arrange(file);
        Assert.Equal(arrangedOnce, File.ReadAllText(file));

        // And the round is not a fixed point for the trivial reason that neither command did anything.
        if (style == "remove") {
            // ⚠ "Always": the repository asked for these parentheses to go, so SK2174 adds none and the
            // arranger takes the one that was already there.
            Assert.Contains("=> a | b & c;", fixedOnce, StringComparison.Ordinal);
            Assert.DoesNotContain("(", Body(arrangedOnce), StringComparison.Ordinal);
        } else {
            Assert.Contains("=> a | (b & c);", fixedOnce, StringComparison.Ordinal);
            Assert.Contains("=> value << (offset + 1);", arrangedOnce, StringComparison.Ordinal);
            Assert.Contains("=> mask & (offset + 1);", arrangedOnce, StringComparison.Ordinal);
            Assert.Contains("=> a | (b & c);", arrangedOnce, StringComparison.Ordinal);
        }
    }

    static void Fix(string file) {
        var run = CliRunner.Run("fix", file, "--load=loose", "--include", "SK2174");
        Assert.True(run.ExitCode == 0, run.StandardOutput + run.StandardError);
    }

    static void Arrange(string file) {
        var run = CliRunner.Run("arrange", file, "--load=loose");
        Assert.True(run.ExitCode == 0, run.StandardOutput + run.StandardError);
    }

    /// <summary>The expression bodies, without the parameter lists' own parentheses.</summary>
    static string Body(string text) =>
        string.Concat(
            text.Split('\n')
                .Where(static line => line.Contains("=>", StringComparison.Ordinal))
                .Select(static line => line[(line.IndexOf("=>", StringComparison.Ordinal) + 2)..])
        );

    sealed class Scratch : IDisposable {
        public Scratch() {
            Root = Path.Combine(Path.GetTempPath(), "skala-cli-fix-arrange", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(Path.Combine(Root, ".git"));
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
