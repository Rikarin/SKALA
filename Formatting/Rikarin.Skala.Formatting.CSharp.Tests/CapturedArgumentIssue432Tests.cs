using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;
using System.Runtime.Loader;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #432, SK-DIV-0187: <c>skala format</c> changed what a program prints. A
///     <c>[CallerArgumentExpression]</c> parameter captures its argument's source text, whitespace and
///     line breaks included, so <c>Check(a   &lt;   b)</c> printed <c>[a   &lt;   b]</c> before formatting
///     and <c>[a &lt; b]</c> after — with an identical token stream, so <c>SK9099</c> never fired.
/// </summary>
/// <remarks>
///     ⚠ Every shape is asserted by <b>compiling and running</b> the program before and after formatting
///     and comparing what it returns, not by comparing text: the claim is about the string the program
///     sees, and a text assertion would pass on a formatter that moved the span and preserved its bytes
///     somewhere the compiler does not capture. Each probe's own output is also asserted to contain the
///     author's mis-spacing, so that a probe whose capture silently stopped working cannot pass by
///     printing the same empty string twice.
/// </remarks>
public sealed class CapturedArgumentIssue432Tests {
    const string Prelude = """
                           using System;
                           using System.Diagnostics;
                           using System.Runtime.CompilerServices;
                           using static Captures;

                           public delegate string Checker(int value, [CallerArgumentExpression("value")] string text = "");

                           public sealed class Box {
                               public Box(int value, [CallerArgumentExpression("value")] string text = "") => Text = text;

                               public string Text { get; }

                               public string this[int value, [CallerArgumentExpression("value")] string text = ""] => "[" + text + "]";
                           }

                           public static class Captures {
                               public static string Check(bool ok, [CallerArgumentExpression(nameof(ok))] string text = "") => "[" + text + "]";

                               public static string Value(int value, [CallerArgumentExpression(nameof(value))] string text = "") =>
                                   "[" + text + "]";

                               public static bool Run(Func<bool> f) => f();

                               public static object? Nothing(bool flag) => null;

                               public static string Many([CallerArgumentExpression("values")] string text = "", params Func<int, int>[] values) =>
                                   "[" + text + "]";

                               public static string Text<T>(this T value, [CallerArgumentExpression("value")] string text = "") =>
                                   "[" + text + "]";

                               public static string Thrown(Action action) {
                                   try {
                                       action();
                                       return "no throw";
                                   } catch (ArgumentException e) {
                                       return "[" + e.ParamName + "]";
                                   }
                               }
                           }

                           public static class Extensions {
                               extension(int number) {
                                   public string Describe([CallerArgumentExpression(nameof(number))] string text = "") => "[" + text + "]";
                               }
                           }

                           public static class Probe {
                               public static string Run() {
                                   int a = 1, b = 2;
                                   bool flag = false;
                                   Checker check = static (value, text) => "[" + text + "]";
                                   var box = new Box(0);
                                   return
                           """;

    const string CapturedSum = "[a   +   b]";

    const string Epilogue = """
                            ;
                                }
                            }

                            """;

    /// <summary>Each row: the return expression, and a fragment its captured output must hold.</summary>
    public static TheoryData<string, string> Shapes() =>
        new() {
            // The issue's own probe.
            { "Captures.Check(a   <   b)", "[a   <   b]" },

            // A break inside the argument, which the formatter would join.
            { "Captures.Check(a <\n                  b)", "<\n" },
            { "Captures.Check(a < b\n                       && b > a)", "b\n                       &&" },

            // Past the margin even on a line of its own, which the formatter would chop at the operators.
            // ⚠ Bare, through the `using static`: written `Captures.Check(…)` the formatter breaks only
            // after the `(` and leaves the chain whole, and the row would pass with the guard removed.
            {
                "Check(a < b && b > a && a != b && a < b && b > a && a != b && a < b && b > a && a != b && a < b && b > a && a != b && a < b && b > a && a != b && a < b)",
                "[a < b && b > a && a != b && a < b && b > a && a != b && a < b && b > a && a != b && a < b && b > a && a != b && a < b && b > a && a != b && a < b]"
            },

            // ⚠ A lambda inside the argument: `csharp_prefer_braces` adds a block to the `if`, which is
            // two tokens in the captured text — the one case here SK9099 would have refused, had it been
            // asked; RequiredBraces runs after the comparison it would have tripped.
            {
                "Captures.Check(Captures.Run(() => {\n                         if (a < b) return true;\n                         return false;\n                   }))",
                "if (a < b) return true;"
            },
            { "Captures.Check(  new[]{1,2}.Length>0  )", "[new[]{1,2}.Length>0]" },

            // ⚠ Measured: the compiler captures an argument without its parentheses, at any depth.
            { "Captures.Check(( a   <   b ))", "[a   <   b]" },
            { "Captures.Value((object)(a  +  b) is int i ? i : 0)", "[(object)(a  +  b) is int i ? i : 0]" },

            // A named argument.
            { "Captures.Check(ok:   a<b)", "[a<b]" },

            // The BCL's capturing members, through the exception's ParamName.
            {
                "Captures.Thrown(() => ArgumentNullException.ThrowIfNull(Captures.Nothing(flag)   ??   Captures.Nothing(flag)))",
                "??   "
            },
            { "Captures.Thrown(() => ArgumentOutOfRangeException.ThrowIfNegative(a   -   b))", "[a   -   b]" },
            { "Captures.Thrown(() => ArgumentOutOfRangeException.ThrowIfGreaterThan(b   +   b, a))", "[b   +   b]" },
            { """Captures.Thrown(() => ArgumentException.ThrowIfNullOrEmpty(flag   ?   "x"   :   ""))""", "?   \"x\"" },

            // A reduced extension call's receiver, and a C# 14 extension block's. ⚠ Measured: the compiler
            // captures a parenthesised receiver without its parentheses.
            { "(a   +   b).Text()", CapturedSum },
            { "(a   +   b).Describe()", CapturedSum },
            { "( a   +   b ).Text()", CapturedSum },

            // A constructor, an indexer and a delegate, each declared in the file.
            { "new Box(a   +   b).Text", "a   +   b" },
            { "box[a   +   b]", CapturedSum },
            { "check(a   +   b)", CapturedSum },
            { "check.Invoke(a   +   b)", CapturedSum },

            // ⚠ A `params` capture is the whole call, measured in #422 — so the call's own spacing is
            // captured too.
            { "Captures.Many(values:   x  =>  x + 5)", "values:   x  =>  x" },

            // A capture nested in an argument that is not captured.
            { "Captures.Value(Captures.Check(a   <   b).Length)", "[Captures.Check(a   <   b).Length]" }
        };

    [Theory]
    [MemberData(nameof(Shapes))]
    public void FormattingLeavesWhatTheProgramReturnsUnchanged(string expression, string captured) {
        var source = Prelude + " " + expression + Epilogue;
        var before = RunProbe(source);
        Assert.Contains(captured, before, StringComparison.Ordinal);

        var result = Format.Run(source);
        Assert.Equal(FormatOutcome.Formatted, result.Outcome);
        Assert.Equal(before, RunProbe(result.Formatted));

        // And it is a fixed point: the second pass changes nothing the first did not.
        Assert.Equal(result.Formatted, Format.Text(result.Formatted));
    }

    /// <summary>
    ///     ⚠ The guard is not "leave every argument alone". The gaps <em>around</em> a captured expression
    ///     are not captured and are still formatted, and an argument nothing captures is formatted in full.
    /// </summary>
    [Theory]
    [InlineData("""Captures.Check(a   <   b, "explicit")""", """Captures.Check(a < b, "explicit")""")]
    [InlineData(
        """Captures.Check(ok: a   <   b, text: "explicit")""",
        """Captures.Check(ok: a < b, text: "explicit")"""
    )]
    [InlineData(
        """Captures.Thrown(() => ArgumentNullException.ThrowIfNull(Captures.Nothing(flag)   ??   b, "p"))""",
        """ThrowIfNull(Captures.Nothing(flag) ?? b, "p")"""
    )]
    [InlineData(
        """Captures.Thrown(() => ArgumentOutOfRangeException.ThrowIfGreaterThan(b   +   b, a, "p"))""",
        """ThrowIfGreaterThan(b + b, a, "p")"""
    )]
    [InlineData("Captures.Value(  a   +   b  ).ToString(  )", "Captures.Value(a   +   b).ToString()")]
    [InlineData("Captures.Run(() =>   a   <   b).ToString()", "Captures.Run(() => a < b).ToString()")]
    [InlineData("Captures.Check(( a   <   b ))", "Captures.Check((a   <   b))")]
    public void WhatIsNotCaptured_IsStillFormatted(string expression, string expected) {
        var formatted = Format.Text(Prelude + " " + expression + Epilogue);

        Assert.Contains(expected, formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Library calls the syntactic answer recognises without a compilation, and ones it must not.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>Debug.Assert</c> cannot be run here: it is <c>[Conditional("DEBUG")]</c>, and the probe is
    ///     compiled without the symbol, so the call — and its capture — is removed. Its row is asserted
    ///     as text. ⚠ xunit's <c>Assert.Equal</c> captures nothing in either major version (measured from
    ///     the assemblies), so its arguments are formatted.
    /// </remarks>
    [Theory]
    [InlineData("Debug.Assert(a   <   b);", "Debug.Assert(a   <   b);")]
    [InlineData("System.Diagnostics.Debug.Assert(a   <   b);", "Debug.Assert(a   <   b);")]
    [InlineData("Trace.Assert(a   <   b);", "Trace.Assert(a   <   b);")]
    [InlineData("""Debug.Assert(a   <   b, "message");""", """Debug.Assert(a < b, "message");""")]
    [InlineData("Assert.That(a   <   b, Is.True);", "Assert.That(a   <   b, Is.True);")]
    [InlineData("Assert.That(a   ,   Is.EqualTo(  b  ));", "Assert.That(a, Is.EqualTo(  b  ));")]
    [InlineData("Assert.Equal(a   ,   b   +   1);", "Assert.Equal(a, b + 1);")]
    [InlineData("ObjectDisposedException.ThrowIf(a   <   b, this);", "ThrowIf(a < b, this);")]
    public void KnownLibraryCalls(string statement, string expected) {
        var source = "class C {\n    void M(int a, int b) {\n        " + statement + "\n    }\n}\n";
        var formatted = Format.Text(source);

        Assert.Contains(expected, formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ <c>using static</c> is the one way a bare <c>Assert(…)</c> binds to <c>Debug.Assert</c>; without
    ///     it the bare name is somebody else's method and is formatted.
    /// </summary>
    [Fact]
    public void ABareCall_IsCapturedOnlyUnderAUsingStatic() {
        const string Body =
            "class C {\n    void M(int a, int b) {\n        Assert(a   <   b);\n    }\n\n    static void Assert(bool c) { }\n}\n";

        Assert.Contains(
            "Assert(a   <   b);",
            Format.Text("using static System.Diagnostics.Debug;\n\n" + Body),
            StringComparison.Ordinal
        );
        Assert.Contains("Assert(a < b);", Format.Text(Body), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ <c>int_align_comments</c> pads the space before a trailing comment, and a trailing comment on
    ///     an interior line of a multi-line captured argument is part of the captured string.
    /// </summary>
    [Fact]
    public void CommentAlignment_DoesNotPadInsideACapturedArgument() {
        var source = Prelude
            + " "
            + "Captures.Check(a < b // first\n            && b > a // a much longer second\n            && a != b)"
            + Epilogue;
        var before = RunProbe(source);
        Assert.Contains("// first\n", before, StringComparison.Ordinal);

        var formatted = FormatWith(source, ("skala_int_align_comments", "true"));
        Assert.Equal(before, RunProbe(formatted));
    }

    /// <summary>
    ///     ⚠ A formatter tag inside a captured argument opens its region as it always has, rather than
    ///     being swallowed by the verbatim chunk — the region runs past the argument, and only the tag
    ///     reaching the emitter opens it.
    /// </summary>
    [Fact]
    public void AFormatterOffTagInsideACapturedArgument_StillTurnsFormattingOff() {
        const string Source =
            "class C {\n    void M(int a, int b) {\n        Debug.Assert(a < b // @formatter:off\n            && b   >   a);\n        int   x   =   1;\n    }\n}\n";

        var formatted = Format.Text(Source);

        Assert.Contains("int   x   =   1;", formatted, StringComparison.Ordinal);
    }

    static string FormatWith(string source, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [.. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
            )
                .Options
        );

        var result = CSharpFormatter.Format("Test.cs", SourceText.From(source), options);
        Assert.Equal(FormatOutcome.Formatted, result.Outcome);
        return result.Formatted;
    }

    /// <summary>Compiles <paramref name="source" /> and returns what <c>Probe.Run()</c> returns.</summary>
    static string RunProbe(string source) {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(static path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "Probe" + Guid.NewGuid().ToString("N"),
            [tree],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        using var image = new MemoryStream();
        var emitted = compilation.Emit(image, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(
            emitted.Success,
            string.Join("\n", emitted.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error))
            + "\n"
            + source
        );

        image.Position = 0;
        var context = new AssemblyLoadContext(null, true);
        try {
            var assembly = context.LoadFromStream(image);
            return (string)assembly.GetType("Probe")!.GetMethod("Run")!.Invoke(null, null)!;
        } finally {
            context.Unload();
        }
    }
}
