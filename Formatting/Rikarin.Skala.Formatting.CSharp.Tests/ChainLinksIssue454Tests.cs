namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issues #454, #455 and #456: what a chained call's links are. Every expected string is
///     <c>jb cleanupcode</c>'s own output for the input, measured 2026-10-08 with <c>Testing ask</c> from
///     flat input under the repository's export; <c>constructs/breaks/chain-links.cs</c> holds the wider
///     set as a fixture.
/// </summary>
/// <remarks>
///     ⚠ One walk, <c>BreakPlan.ChainLinks</c>, answered all three wrong, and the three were filed as
///     separate entries (SK-DIV-0066, SK-DIV-0067, SK-DIV-0068, SK-DIV-0184). A chain whose outermost
///     node is a member access was never a chain root, so <c>….ToList().Count</c> planned no group and the
///     last argument list took the break. A property run feeding a call stopped at a <c>?</c>. And the
///     walk went through a <c>!</c>, counting the call before it as one of the chain's.
/// </remarks>
public sealed class ChainLinksIssue454Tests {
    const string Long1 = ".Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentVa"
        + "luexxxxxxxxx);";

    static string Statement(string statement) =>
        $$"""
          class T {
              void M() {
                  {{statement}}
              }
          }
          """;

    /// <summary>
    ///     #454: a chain ending in a property run breaks before the run's first dot, a <c>?.</c> inside the
    ///     run included. Before the fix: <c>….OrderBy(n =&gt; n</c> / <c>).ToList().Count;</c>.
    /// </summary>
    [Theory]
    [InlineData(
        "var result = someCollectionOfThingsHere.Where(c => c.IsEnabled).Select(c => c.Name).OrderBy(n => n).ToList().Count;",
        """
        var result = someCollectionOfThingsHere.Where(c => c.IsEnabled)
                    .Select(c => c.Name)
                    .OrderBy(n => n)
                    .ToList()
                    .Count;
        """
    )]
    [InlineData(
        "var y2 = source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaa).Where(beta).Count.Value;",
        """
        var y2 = source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaa)
                    .Where(beta)
                    .Count.Value;
        """
    )]
    [InlineData(
        "var y3 = source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaa).Where(beta).Count?.Value;",
        """
        var y3 = source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaa)
                    .Where(beta)
                    .Count?.Value;
        """
    )]
    [InlineData(
        "var a = SomeMethod(aaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccccc).Property;",
        """
        var a = SomeMethod(aaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccccc)
                    .Property;
        """
    )]
    public void AChainEndingInAProperty_BreaksBeforeTheRun(string input, string expected) =>
        Oracle.Agrees(Statement(input), Statement(expected));

    /// <summary>
    ///     #455: a <c>!</c> ends the receiver, so the call after it is the chain's first and stays with
    ///     it; and the operand of the <c>!</c> breaks as a chain of its own. Before the fix:
    ///     <c>….SelfLink()!</c> / <c>.SelfLink()</c>, and <c>.Where(gamma…)!</c> / <c>.Where(beta)</c>.
    /// </summary>
    [Fact]
    public void ABang_EndsTheReceiver() {
        Oracle.Agrees(
            Statement(
                "var bang = receiverWithAVeryLongNameIndeed.SelfLink()!.SelfLink().SelfLink().SelectName(n => n.Name).ToList().Count();"
            ),
            Statement(
                """
                var bang = receiverWithAVeryLongNameIndeed.SelfLink()!.SelfLink()
                            .SelfLink()
                            .SelectName(n => n.Name)
                            .ToList()
                            .Count();
                """
            )
        );

        Oracle.Agrees(
            Statement(
                "var x4 = sourceeeeeeeeeeeeeeeeeeeee.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo).Where(gammaArgumentValuexxxxx)!.Where(beta).ToList();"
            ),
            Statement(
                """
                var x4 = sourceeeeeeeeeeeeeeeeeeeee.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo)
                            .Where(gammaArgumentValuexxxxx)!.Where(beta)
                            .ToList();
                """
            )
        );
    }

    /// <summary>
    ///     #455: <c>?[0]</c> is an indexer at the head, as <c>[0]</c> is; and an indexed property after a
    ///     call is a link of its own. Before the fix: <c>sourceWithAVeryLongName?[0].Children.Where(…)</c>
    ///     on one line, and <c>source.Make().Items[0]</c> on one.
    /// </summary>
    [Fact]
    public void AnIndexer_IsACall() {
        Oracle.Agrees(
            Statement(
                "var elemc = sourceWithAVeryLongName?[0].Children.Where(item => item.IsEnabled).Select(item => item.Name).ToList();"
            ),
            Statement(
                """
                var elemc = sourceWithAVeryLongName?[0]
                            .Children.Where(item => item.IsEnabled)
                            .Select(item => item.Name)
                            .ToList();
                """
            )
        );

        Oracle.Agrees(
            Statement(
                "var x6 = source.Make().Items[0].Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValuexxxxxxxxx);"
            ),
            Statement(
                $$"""
                  var x6 = source.Make()
                              .Items[0]
                              {{Long1}}
                  """
            )
        );
    }

    /// <summary>
    ///     #456: a property run feeding a call reaches left across a <c>?</c> — one property, two, a
    ///     second <c>?</c>, a leading <c>?.</c>. Before the fix: <c>….Self().Inner</c> /
    ///     <c>?.Children.Where(…)</c>.
    /// </summary>
    [Theory]
    [InlineData(
        "var c = someParticularThingWithALongName.Self().Inner?.Children.Where(item => item.IsEnabled).Select(item => item.Name).ToList();",
        """
        var c = someParticularThingWithALongName.Self()
                    .Inner?.Children.Where(item => item.IsEnabled)
                    .Select(item => item.Name)
                    .ToList();
        """
    )]
    [InlineData(
        "var c2 = someParticularThingWithALongName.Self().Outer.Inner?.Children.Where(item => item.IsEnabled).Select(item => item.Name);",
        """
        var c2 = someParticularThingWithALongName.Self()
                    .Outer.Inner?.Children.Where(item => item.IsEnabled)
                    .Select(item => item.Name);
        """
    )]
    [InlineData(
        "var c3 = someParticularThingWithALongName.Self().Inner?.Children?.Where(item => item.IsEnabled).Select(item => item.Name).ToList();",
        """
        var c3 = someParticularThingWithALongName.Self()
                    .Inner?.Children?.Where(item => item.IsEnabled)
                    .Select(item => item.Name)
                    .ToList();
        """
    )]
    [InlineData(
        "var c4 = someParticularThingWithALongName.Self()?.Inner?.Children.Where(item => item.IsEnabled).Select(item => item.Name).ToList();",
        """
        var c4 = someParticularThingWithALongName.Self()
                    ?.Inner?.Children.Where(item => item.IsEnabled)
                    .Select(item => item.Name)
                    .ToList();
        """
    )]
    public void APropertyRun_ReachesAcrossAQuestionMark(string input, string expected) =>
        Oracle.Agrees(Statement(input), Statement(expected));
}
