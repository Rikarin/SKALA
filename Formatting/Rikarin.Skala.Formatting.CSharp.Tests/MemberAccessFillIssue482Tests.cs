namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #482: a member-access expression that is no chain of calls breaks once, at the last dot that
///     fits. Every expected string is <c>jb cleanupcode</c>'s own output, measured 2026-10-08 with
///     <c>Testing ask</c> under the repository's export; <c>constructs/breaks/member-access-fill.cs</c>
///     holds the wider set, the positions that decline the fill included.
/// </summary>
public sealed class MemberAccessFillIssue482Tests {
    const string Head = "Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd";

    /// <summary>
    ///     The issue's arm: the last dot breaks rather than the arrow, while the arrow still breaks for a
    ///     body of fourteen columns. Before the fix: <c>…MorexxxxxxxxxxxxxxxxxValue =&gt;</c> / <c>2u,</c>.
    /// </summary>
    [Fact]
    public void ASwitchArmPattern_BreaksAtItsLastDot_BeforeAShortBody() =>
        Oracle.Agrees(
            $$"""
              class T {
                  int N(object o) =>
                      o switch {
                          {{Head}}.MorexxxxxxxxxxxxxxxxxValue => 2u,
                          {{Head}}.MorexxxxxxxxxxxValue => yyyyyyyyyyyyy,
                          {{Head}}.MorexxxxxxxxxxxValue => yyyyyyyyyyyyyy,
                          _ => 0
                      };
              }
              """,
            $$"""
              class T {
                  int N(object o) =>
                      o switch {
                          {{Head}}
                              .MorexxxxxxxxxxxxxxxxxValue => 2u,
                          {{Head}}
                              .MorexxxxxxxxxxxValue => yyyyyyyyyyyyy,
                          {{Head}}.MorexxxxxxxxxxxValue =>
                              yyyyyyyyyyyyyy,
                          _ => 0
                      };
              }
              """
        );

    /// <summary>
    ///     After an <c>=</c> the last dot that fits breaks rather than the <c>=</c>, and what follows it
    ///     stays on the continuation line. Before the fix: <c>var value =</c> / the whole chain.
    /// </summary>
    [Fact]
    public void AValue_BreaksAtTheLastDotThatFits() =>
        Oracle.Agrees(
            $$"""
              class T {
                  void M() {
                      var value = {{Head}}.MorexxxxxxxxxxxxxxxxxValueeee;
                      var value2 = {{Head}}.MorexxxxxxxxxxxxxxxxxValueeee.Rest;
                  }
              }
              """,
            $$"""
              class T {
                  void M() {
                      var value = {{Head}}
                          .MorexxxxxxxxxxxxxxxxxValueeee;
                      var value2 = {{Head}}
                          .MorexxxxxxxxxxxxxxxxxValueeee.Rest;
                  }
              }
              """
        );
}
