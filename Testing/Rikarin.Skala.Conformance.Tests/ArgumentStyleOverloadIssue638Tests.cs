using Rikarin.Skala.Formatting.CSharp.Arrangement;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     ⚠ #638. <c>SK0216</c> adds or removes an argument's name only where overload resolution still lands
///     on the same member afterwards.
/// </summary>
/// <remarks>
///     The rule used to decide by the bound overload's own parameter list: a name was redundant when it
///     matched the parameter at its position. The other overloads in the group are what decide it. Two
///     shapes from Vixen (xunit's <c>Assert.Equal(…, tolerance: 1)</c> and an overloaded loader) turned
///     into CS0121, which safety layer 2 caught and reported as <c>SK9098</c> — and that refusal kept
///     <c>skala baseline create</c> from writing anything. ⚠ The rows that matter most are the silent
///     ones: <c>Widen(a: 1, x: 2)</c> unnamed binds a <em>different</em> overload and compiles, so no
///     safety layer sees it. Those rows assert on the text, because the outcome alone would be green.
/// </remarks>
public sealed class ArgumentStyleOverloadIssue638Tests {
    const string Preamble = """
                            using System;
                            using System.Collections.Generic;
                            using System.Threading;
                            using static P.Overloads;

                            namespace P;

                            public sealed class Composition;

                            public static class Overloads {
                                public static int Load(Composition composition, CancellationToken token) => 1;
                                public static int Load(IEnumerable<string> names, CancellationToken token) => 2;

                                public static bool Equal(float expected, float actual, int precision) => true;
                                public static bool Equal(decimal expected, decimal actual, int precision) => true;
                                public static bool Equal(float expected, float actual, float tolerance) => true;

                                public static int Widen(long a, int x) => 1;
                                public static int Widen(int b, int x) => 2;

                                public static int Put<T>(T item, int x) => 1;
                                public static int Put(long other, int x) => 2;

                                public static int Mixed(int a, object b) => 1;
                                public static int Mixed(string b, int a) => 2;

                                public static int Swapped(int a, string b) => 1;
                                public static int Swapped(string b, int a) => 2;

                                public static int Lone(int a, string b) => 1;
                                public static int Many(int first, params int[] rest) => 1;
                                public static int Optional(int a, int b = 0, int c = 0) => 1;

                                public static int Size(this string s, long wide, int x) => 1;
                                public static int Size(this string s, int narrow, int x) => 2;
                            }

                            public sealed class Holder {
                                public int Load(Composition composition, CancellationToken token) => 1;
                                public int Load(IEnumerable<string> names, CancellationToken token) => 2;
                            }

                            public class Loader {
                                public Loader(string path, int depth) { }
                                public Loader(Uri? uri, int depth) { }
                            }

                            """;

    static readonly KeyValuePair<string, string>[] Positional = [
        new("skala_arguments_literal", "positional"),
        new("skala_arguments_string_literal", "positional"),
        new("skala_arguments_anonymous_function", "positional"),
        new("skala_arguments_named", "positional"),
        new("skala_arguments_other", "positional"),
        new("skala_arguments_skip_single", "false")
    ];

    static readonly KeyValuePair<string, string>[] Named = [
        new("skala_arguments_literal", "named"),
        new("skala_arguments_string_literal", "named"),
        new("skala_arguments_anonymous_function", "positional"),
        new("skala_arguments_named", "positional"),
        new("skala_arguments_other", "positional"),
        new("skala_arguments_skip_single", "false")
    ];

    /// <summary>Both shapes from the issue, verbatim: nothing to arrange, and nothing reported.</summary>
    [Fact]
    public void TheIssuesTwoCalls_AreLeftAloneAndNotReported() {
        const string source = """
                              using System.Collections.Generic;
                              using System.Threading;

                              namespace P;

                              public sealed class Composition;

                              public static class Repro {
                                  public static int Load(Composition composition, CancellationToken token) => 1;
                                  public static int Load(IEnumerable<string> names, CancellationToken token) => 2;

                                  public static int Pick() => Load(composition: null!, CancellationToken.None);

                                  public static bool Equal(float expected, float actual, int precision) => true;
                                  public static bool Equal(decimal expected, decimal actual, int precision) => true;
                                  public static bool Equal(float expected, float actual, float tolerance) => true;

                                  public static bool Check(int a, int b) => Equal(a, b, tolerance: 1);
                              }
                              """;

        var result = ArrangementRuleTests.Attempt(source, ArrangeIds.ArgumentStyle, overrides: Positional);

        Assert.Equal(source, ArrangementRuleTests.Declined(result));
        Assert.DoesNotContain(ArrangeIds.ArgumentStyle, result.Applied);
    }

    /// <summary>A name the overload needs stays; where the call has several, the others still go.</summary>
    [Theory]
    // Unnamed, these are CS0121 — the two shapes of the issue, and the same in each kind of call.
    [InlineData(
        "public int M() => Load(composition: null!, CancellationToken.None);",
        "Load(composition: null!, CancellationToken.None)"
    )]
    [InlineData("public bool M(int a, int b) => Equal(a, b, tolerance: 1);", "Equal(a, b, tolerance: 1)")]
    [InlineData("public Loader M() => new Loader(path: null!, 1);", "new Loader(path: null!, 1)")]
    [InlineData("public Loader M() => new(path: null!, 1);", "new(path: null!, 1)")]
    [InlineData("public Probe(int n) : base(path: null!, n) { }", "base(path: null!, n)")]
    [InlineData(
        "public int? M(Holder? h) { return h?.Load(composition: null!, CancellationToken.None); }",
        "h?.Load(composition: null!, CancellationToken.None)"
    )]
    // ⚠ Unnamed, these compile and call a different overload — the `int` one, the generic one.
    [InlineData("public int M() => Widen(a: 1, x: 2);", "Widen(a: 1, 2)")]
    [InlineData("public int M() => Put(other: 1, x: 2);", "Put(other: 1, 2)")]
    [InlineData("public int M(string s) => s.Size(wide: 1, x: 2);", "s.Size(wide: 1, 2)")]
    public void ANameThatSelectsTheOverload_IsKept(string member, string expected) =>
        Assert.Contains(expected, Arranged(member, Positional), StringComparison.Ordinal);

    /// <summary>The other direction: a name added can bring an overload into play.</summary>
    /// <remarks>
    ///     ⚠ <c>Mixed(a: 1, b: "x")</c> binds <c>Mixed(string b, int a)</c> — a better match than the
    ///     overload <c>Mixed(1, "x")</c> calls, which compiles and changes what runs. <c>Swapped</c> with
    ///     both names is CS0121. In both the first name is out of place in the second overload, so it
    ///     alone keeps the call where it was, and that one is added.
    /// </remarks>
    [Theory]
    [InlineData("public int M() => Mixed(1, \"x\");", "Mixed(a: 1, \"x\")")]
    [InlineData("public int M() => Swapped(1, \"x\");", "Swapped(a: 1, \"x\")")]
    public void ANameThatWouldSelectAnotherOverload_IsNotAdded(string member, string expected) =>
        Assert.Contains(Unspaced(expected), Unspaced(Arranged(member, Named)), StringComparison.Ordinal);

    /// <summary>Overloaded or not, a name the resolution does not depend on still goes.</summary>
    [Theory]
    [InlineData(
        "public int M(Composition c) => Load(composition: c, token: CancellationToken.None);",
        "Load(c, CancellationToken.None)"
    )]
    [InlineData(
        "public bool M(float a, float b) => Equal(expected: a, actual: b, precision: 1);",
        "Equal(a, b, 1)"
    )]
    [InlineData("public int M() => Widen(b: 1, x: 2);", "Widen(1, 2)")]
    [InlineData("public int M() => Put(item: \"s\", x: 2);", "Put(\"s\", 2)")]
    [InlineData("public int M(string s) => s.Size(narrow: 1, x: 2);", "s.Size(1, 2)")]
    [InlineData("public Loader M() => new Loader(path: \"p\", depth: 1);", "new Loader(\"p\", 1)")]
    [InlineData("public Loader M() => new(path: \"p\", depth: 1);", "new(\"p\", 1)")]
    [InlineData("public Probe(int n) : base(path: \"p\", depth: n) { }", "base(\"p\", n)")]
    [InlineData(
        "public int? M(Holder? h, Composition c) { return h?.Load(composition: c, token: CancellationToken.None); }",
        "h?.Load(c, CancellationToken.None)"
    )]
    [InlineData("public int M(Func<int, int, int> f) => f(arg1: 1, arg2: 2);", "f(1, 2)")]
    [InlineData("public int M() { int L(int a, int b) => a; return L(a: 1, b: 2); }", "L(1, 2)")]
    [InlineData("public int M() => Many(first: 1, 2, 3);", "Many(1, 2, 3)")]
    [InlineData("public int M() => Optional(a: 1, b: 2);", "Optional(1, 2)")]
    public void ANameTheOverloadDoesNotNeed_IsRemoved(string member, string expected) =>
        Assert.Contains(expected, Arranged(member, Positional), StringComparison.Ordinal);

    [Theory]
    [InlineData("public int M() => Lone(1, \"x\");", "Lone(a: 1, b: \"x\")")]
    [InlineData("public Loader M() => new(\"p\", 1);", "new(path: \"p\", depth: 1)")]
    public void ANameTheOverloadAllows_IsAdded(string member, string expected) =>
        Assert.Contains(Unspaced(expected), Unspaced(Arranged(member, Named)), StringComparison.Ordinal);

    /// <summary>
    ///     <paramref name="member" /> in a class deriving from <c>Loader</c>, arranged by <c>SK0216</c>
    ///     alone, with the run asserted never to have reached the safety layer.
    /// </summary>
    static string Arranged(string member, KeyValuePair<string, string>[] overrides) =>
        ArrangementRuleTests.Declined(
            ArrangementRuleTests.Attempt(
                Preamble
                + "public class Probe : Loader {\n"
                + "    public Probe() : base(\"\", 0) { }\n\n"
                + "    "
                + member
                + "\n"
                + "}\n",
                ArrangeIds.ArgumentStyle,
                overrides: overrides
            )
        );

    /// <summary>
    ///     ⚠ The arranger writes an added name as <c>a:1</c> and leaves the space to the formatter, so a
    ///     row about which names were added compares with the spaces taken out.
    /// </summary>
    static string Unspaced(string text) => text.Replace(" ", string.Empty, StringComparison.Ordinal);
}
