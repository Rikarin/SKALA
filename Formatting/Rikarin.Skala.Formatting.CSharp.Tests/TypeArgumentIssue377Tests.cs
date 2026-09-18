namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #377: a parameter whose type argument list holds another list in an earlier argument, with
///     or without an attribute in front. Every expected string is <c>jb cleanupcode</c>'s own output
///     for the input, measured 2026-09-18 with <c>Testing ask</c>, except where a remark says which of
///     the oracle's two answers it is; <c>constructs/breaks/nested-list-in-a-type-argument.cs</c> holds
///     the shapes the oracle answers once as a fixture.
/// </summary>
/// <remarks>
///     ⚠ The Nightly fuzzer's seed 6103756300633105773 found two mechanisms, and the issue's guess
///     named neither exactly. The type argument list's points were <em>last-resort</em> ones, read
///     through by everything before them — the list's own first argument included — so the
///     <c>List&lt;Guid&gt;</c> nested in it measured the whole 145-column parameter, broke at its own
///     <c>&lt;</c>, and the outer comma the oracle takes was left to break as well; no tuple was needed
///     (<c>Dictionary&lt;Dictionary&lt;A, List&lt;B&gt;&gt;, List&lt;…&gt;&gt;</c> did the same). Pass two then read
///     that break as the author's, which shortened the attribute section's measure and joined the
///     <c>]</c> gap that pass one had broken. ⚠ The second mechanism is the oracle's too: given the flat
///     input it puts <c>[Obsolete]</c> on its own line, and given its own output it joins the two,
///     because the kept comma break now ends the measure. Its fixed point is the joined form, and the
///     rule it settles on is "joined exactly when <c>[Obsolete] </c> plus the parameter's first line, as
///     laid out alone, fits". Skala takes that answer in one pass from either input, so on the flat
///     input alone it differs from the oracle's first pass — SK-DIV-0119 records the choice. A section
///     that spans lines is not part of it: the parameter goes below it whatever its width, which
///     <c>AttributeSectionTests</c> pins and which the first cut of the join broke.
/// </remarks>
public sealed class TypeArgumentIssue377Tests {
    const string Constrained = "internal sealed record T1<T2, T3, T4>"
        + " where T2 : class, IEquatable<T2>, new() where T3 : notnull {";

    const string Plain = "internal sealed record T1<T2, T3, T4> {";
    const string FirstTwo = "Dictionary<string, (char First, StringBuilder? Second)> p13, ValueTask<int> p14, ";

    const string Nested = "Dictionary<(Dictionary<Guid, List<Guid>> First, StringBuilder Second),"
        + " List<(Dictionary<Guid, List<Guid>> First, StringBuilder Second)>>";

    const string One = "SomeVeryLongTypeNameNumberOne";
    const string Two = "SomeVeryLongTypeNameNumberTwo";

    const string Fits = "[Obsolete] Dictionary<(Guid First, StringBuilder Second),"
        + " List<(Guid First, StringBuilder Second)>> p15";

    /// <summary>The fuzzer's minimised input, with <paramref name="third" /> as the third parameter.</summary>
    static string Method(string third, string header = Constrained) =>
        $$"""
          {{header}}
            public sealed struct T10 {
            public static async Task M12<T11>({{FirstTwo}}{{third}}) {
            }
            }
          }
          """;

    /// <summary>The chopped parameter list every answer below shares, around <paramref name="third" />.</summary>
    static string Chopped(string third, string header = Constrained) =>
        $$"""
          {{header}}
              public sealed struct T10 {
                  public static async Task M12<T11>(
                      Dictionary<string, (char First, StringBuilder? Second)> p13,
                      ValueTask<int> p14,
          {{third}}
                  ) { }
              }
          }
          """;

    /// <summary>
    ///     The fuzzer's input. One pass equals two and equals the oracle's fixed point: the outer list
    ///     breaks at its comma, nothing nested breaks, and <c>[Obsolete]</c> stays on the parameter's
    ///     line because <c>[Obsolete] Dictionary&lt;(…Second),</c> is 92 columns.
    /// </summary>
    [Fact]
    public void TheFuzzersInput_SettlesInOnePass_OnTheOraclesFixedPoint() {
        var fixedPoint = Chopped(
            """
                        [Obsolete] Dictionary<(Dictionary<Guid, List<Guid>> First, StringBuilder Second),
                            List<(Dictionary<Guid, List<Guid>> First, StringBuilder Second)>> p15
            """
        );

        var once = Format.Text(Method($"[Obsolete] {Nested} p15"));
        Assert.Equal(fixedPoint, once.TrimEnd('\n'));
        Assert.Equal(once, Format.Text(once));

        // ⚠ The oracle's first answer to the same input, and its second: `[Obsolete]` alone with the
        // list below it, then the two joined. The joined form is what it returns unchanged, and Skala
        // takes the oracle's first answer there too — as the oracle itself does.
        var oraclePassOne = Chopped(
            """
                        [Obsolete]
                        Dictionary<(Dictionary<Guid, List<Guid>> First, StringBuilder Second),
                            List<(Dictionary<Guid, List<Guid>> First, StringBuilder Second)>> p15
            """
        );

        Assert.Equal(fixedPoint, Format.Text(oraclePassOne).TrimEnd('\n'));
        Oracle.Agrees(fixedPoint, fixedPoint);
    }

    /// <summary>
    ///     What pass one used to write — the nested <c>List&lt;↵Guid&gt;&gt;</c> break and the section alone
    ///     — settles in one pass with the section joined: the nested break is a kept one now, so it
    ///     stays, and the section joins across it. ⚠ The oracle does exactly that with this input, at a
    ///     deeper continuation for the kept break (<c>Guid&gt;&gt;</c> two levels in); the line that
    ///     matters, the <c>[Obsolete] Dictionary&lt;(</c> join, is the oracle's.
    /// </summary>
    [Fact]
    public void TheOldPassOne_JoinsTheSection_AsTheOracleDoes() {
        var oldPassOne = Chopped(
            """
                        [Obsolete]
                        Dictionary<(Dictionary<Guid, List<
                            Guid>> First, StringBuilder Second),
                            List<(Dictionary<Guid, List<Guid>> First, StringBuilder Second)>> p15
            """
        );

        var once = Format.Text(oldPassOne);
        Assert.Equal(once, Format.Text(once));
        Assert.Contains("            [Obsolete] Dictionary<(Dictionary<Guid, List<\n", once, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Without the attribute the oracle's one answer is the outer comma, with every nested list
    ///     whole — the first mechanism on its own. The <c>where</c> clauses change nothing.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithoutTheAttribute_TheOuterCommaBreaks_AndNothingNested(bool constraints) {
        var header = constraints ? Constrained : Plain;
        Oracle.Agrees(
            Method($"{Nested} p15", header),
            Chopped(
                """
                            Dictionary<(Dictionary<Guid, List<Guid>> First, StringBuilder Second),
                                List<(Dictionary<Guid, List<Guid>> First, StringBuilder Second)>> p15
                """,
                header
            )
        );
    }

    /// <summary>
    ///     No tuple anywhere: the same break at the outer comma, so the tuple type the issue named was
    ///     incidental. Before the fix Skala broke <c>List&lt;↵SomeVeryLongTypeNameNumberTwo&gt;&gt;,</c> here.
    /// </summary>
    [Fact]
    public void WithoutTheTuple_TheOuterCommaBreaks() =>
        Oracle.Agrees(
            Method($"Dictionary<Dictionary<{One}, List<{Two}>>, List<Dictionary<{One}, {Two}>>> p15", Plain),
            Chopped(
                $$"""
                              Dictionary<Dictionary<{{One}}, List<{{Two}}>>,
                                  List<Dictionary<{{One}}, {{Two}}>>> p15
                  """,
                Plain
            )
        );

    /// <summary>A parameter that fits after its section stays whole, section and all.</summary>
    [Fact]
    public void AParameterThatFits_KeepsItsSectionAndItsList() =>
        Oracle.Agrees(Method($"{Fits}", Plain), Chopped($"            {Fits}", Plain));

    /// <summary>The same list as a return type and as a local's type: the outer comma, one level in.</summary>
    [Fact]
    public void AReturnTypeAndALocal_BreakAtTheOuterComma() =>
        Oracle.Agrees(
            $$"""
              internal sealed record T1<T2, T3, T4> {
                public sealed struct T10 {
                public static {{Nested}} M13() {
                }
                public static void M14() {
                {{Nested}} local = null;
                var created = new {{Nested}}();
                }
                }
              }
              """,
            """
            internal sealed record T1<T2, T3, T4> {
                public sealed struct T10 {
                    public static Dictionary<(Dictionary<Guid, List<Guid>> First, StringBuilder Second),
                        List<(Dictionary<Guid, List<Guid>> First, StringBuilder Second)>> M13() { }

                    public static void M14() {
                        Dictionary<(Dictionary<Guid, List<Guid>> First, StringBuilder Second),
                            List<(Dictionary<Guid, List<Guid>> First, StringBuilder Second)>> local = null;
                        var created =
                            new Dictionary<(Dictionary<Guid, List<Guid>> First, StringBuilder Second),
                                List<(Dictionary<Guid, List<Guid>> First, StringBuilder Second)>>();
                    }
                }
            }
            """
        );

    /// <summary>
    ///     The boundary of the section's rule, from the oracle's own fixed points. A three-argument list
    ///     107 columns wide fits alone at column 12 and not after <c>[Obsolete] </c>, so the section
    ///     stays alone and the list stays whole; a four-argument list whose first line alone is 103
    ///     columns keeps the section alone as well, and both answers are the oracle's first and second
    ///     alike. ⚠ A rule measuring the parameter's first <em>argument</em> instead would join both.
    /// </summary>
    [Fact]
    public void TheSectionStaysAlone_WhenTheParametersFirstLineDoesNotFitBesideIt() {
        Oracle.Agrees(
            Method($"[Obsolete] Dictionary<{One}, {Two}, {Two}> p15", Plain),
            Chopped(
                $$"""
                              [Obsolete]
                              Dictionary<{{One}}, {{Two}}, {{Two}}> p15
                  """,
                Plain
            )
        );

        Oracle.Agrees(
            Method($"[Obsolete] Dictionary<{One}, {Two}, {Two}, {Two}> p15", Plain),
            Chopped(
                $$"""
                              [Obsolete]
                              Dictionary<{{One}}, {{Two}}, {{Two}},
                                  {{Two}}> p15
                  """,
                Plain
            )
        );
    }

    /// <summary>
    ///     A break the author wrote after the <c>]</c>, with the parameter flat: the oracle's first pass
    ///     keeps it and its second joins it, and Skala joins it — the same fixed point as from the flat
    ///     input, which is the point of deciding it by the output.
    /// </summary>
    [Fact]
    public void AKeptBreakAfterTheBracket_ReachesTheSameFixedPoint() {
        var once = Format.Text(Method($"[Obsolete]\n  {Nested} p15", Plain));
        Assert.Equal(once, Format.Text(once));
        Assert.Contains(
            "            [Obsolete] Dictionary<(Dictionary<Guid, List<Guid>> First, StringBuilder Second),\n",
            once,
            StringComparison.Ordinal
        );
    }
}
