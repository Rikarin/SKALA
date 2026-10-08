using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #531: the member-access fill's residues. Every expected string is <c>jb cleanupcode</c>'s own
///     output, measured 2026-10-08 with <c>Testing ask</c>.
/// </summary>
public sealed class MemberAccessFillIssue531Tests {
    const string Long3 = "Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd.Mo"
        + "rexxxxValue => yyyyyyyyyyyyy,";

    const string Long1 = "Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd.Mo"
        + "rexxxxxxxxxxxValue => yyyyyyyyyyy.Z,";

    const string Long2 = "Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd.Mo"
        + "rexxxxxxxxxxxxxxxxxxxxValueeeeee => yyyyyyyyyyyyyyyyyy,";

    const string Head = "Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd";

    /// <summary>
    ///     An assignment's target that overflows by itself breaks at its last dot that fits and keeps its
    ///     <c>=</c>; one that fits with its <c>=</c> breaks after the <c>=</c>. Before the fix the first came
    ///     back <c>….Valueeee =</c> / <c>1;</c> past the margin.
    /// </summary>
    [Fact]
    public void AnAssignmentsTarget_BreaksAtItsDot_OnlyWhenItOverflowsByItself() =>
        Oracle.Agrees(
            $$"""
              class T {
                  void M() {
                      {{Head}}.Morexxxxxxxxxxxxxxxx.Valueeeeeeeeeeeeeeeee = 1;
                      {{Head}}.MorexxxxxxxxxxxxxxxValue = yyyyyyyy;
                  }
              }
              """,
            $$"""
              class T {
                  void M() {
                      {{Head}}.Morexxxxxxxxxxxxxxxx
                          .Valueeeeeeeeeeeeeeeee = 1;
                      {{Head}}.MorexxxxxxxxxxxxxxxValue =
                          yyyyyyyy;
                  }
              }
              """
        );

    /// <summary>
    ///     A switch arm's last arm, without a comma, fills at fourteen columns of body; with a comma the
    ///     arrow breaks. Before the fix both broke the arrow.
    /// </summary>
    [Fact]
    public void AnArmsBody_IsMeasuredWithItsComma() =>
        Oracle.Agrees(
            $$"""
              class T {
                  object N(object o) =>
                      o switch {
                          {{Head}}.MorexxxxxxxxxxxValue => yyyyyyyyyyyyyy,
                          {{Head}}.MorexxxxxxxxxxxValueeeeee => yyyyyyyyyyyyyy
                      };
              }
              """,
            $$"""
              class T {
                  object N(object o) =>
                      o switch {
                          {{Head}}.MorexxxxxxxxxxxValue =>
                              yyyyyyyyyyyyyy,
                          {{Head}}
                              .MorexxxxxxxxxxxValueeeeee => yyyyyyyyyyyyyy
                      };
              }
              """
        );

    /// <summary>
    ///     The three arm rows round two left open (SK-DIV-0330), now answered by the arm's table: a
    ///     fourteen-column body with its comma behind a 106-column head breaks the arrow; a body with a dot
    ///     of its own fills the pattern; and a pattern that overflows by itself breaks its own dot whatever
    ///     the body. Before the fix the three came out the other way.
    /// </summary>
    [Fact]
    public void TheArmRows_FollowTheArmsTable() =>
        Oracle.Agrees(
            $$"""
              class T {
                  object N(object o) =>
                      o switch {
                          {{Long3}}
                          {{Long1}}
                          {{Long2}}
                          _ => 0
                      };
              }
              """,
            """
            class T {
                object N(object o) =>
                    o switch {
                        Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd.MorexxxxValue =>
                            yyyyyyyyyyyyy,
                        Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd
                            .MorexxxxxxxxxxxValue => yyyyyyyyyyy.Z,
                        Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd
                            .MorexxxxxxxxxxxxxxxxxxxxValueeeeee => yyyyyyyyyyyyyyyyyy,
                        _ => 0
                    };
            }
            """
        );
}
