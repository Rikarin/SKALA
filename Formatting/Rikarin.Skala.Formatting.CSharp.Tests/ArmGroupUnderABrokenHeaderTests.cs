namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     An arm's group opened at its pattern takes the arm's level under a member whose header broke
///     (fuzz 11550439650966795547).
/// </summary>
/// <remarks>
///     ⚠ The input is the oracle's own answer, asked 2026-10-09: it comes back as written, every arrow one level
///     past its arm. Skala put the arrows on the arm's column, the arm's frame not yet started when the group
///     opened.
/// </remarks>
public sealed class ArmGroupUnderABrokenHeaderTests {
    [Fact]
    public void AKeptArrowBreak_TakesTheArmsLevel_UnderABrokenMemberHeader() {
        const string source = """
                              class C576b {
                                  object
                                      C(object owner) =>
                                      owner switch {
                                          DateTime { P25 : not null } when MaterialiseSomethingLongerStill(owner, owner, owner, owner, owner, owner)
                                              => (from item in items where "s" select item),
                                      };

                                  object
                                      D(object owner) =>
                                      owner switch {
                                          DateTime { P25: not null } when MaterialiseSomethingLongerStill(owner, owner, owner, owner, owner, owner)
                                              => Call(item),
                                          _ => 0
                                      };

                                  object
                                      F(object owner) =>
                                      owner switch {
                                          DateTime x when MaterialiseSomethingLongerStill(owner, owner, owner, owner, owner, owner)
                                              => Call(item),
                                          _ => 0
                                      };
                              }
                              """;
        Oracle.Agrees(source, source);
    }
}
