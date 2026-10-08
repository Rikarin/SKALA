namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #457: a chopped chain that is the left operand of a broken binary operator nests from the
///     operator's continuation line — dots two levels past the statement, the operator one. Every
///     expected string is <c>jb cleanupcode</c>'s own output, measured 2026-10-08 with
///     <c>Testing ask</c>; <c>constructs/breaks/chain-in-a-left-operand.cs</c> holds the wider set.
/// </summary>
/// <remarks>
///     ⚠ The chain's own level and the operator's open on the same line, and the writer's
///     one-level-per-opening-line rule collapsed them. The chain's scope now lifts past a broken
///     operator around it, as an argument list on that line already did (#418); a chain that is the
///     right operand opens on the operator's own line and is untouched.
/// </remarks>
public sealed class ChainInALeftOperandIssue457Tests {
    const string Chain =
        "someParticularThingWithALongName.SelfLink().SelfLink().SelectName(n => n.Name).WhereSomething(x => x.IsEnabledAndReady)";

    [Fact]
    public void TheChainsDots_NestFromTheOperatorsLine() =>
        Oracle.Agrees(
            $$"""
              class T {
                  bool M() {
                      var w = {{Chain}} + otherFallbackValueName.SomeFallbackProperty;
                      var v = {{Chain}} ?? otherFallbackValueName.SomeFallbackProperty;
                      return {{Chain}} && otherFallbackValueName.SomeFallbackProperty;
                  }
              }
              """,
            """
            class T {
                bool M() {
                    var w = someParticularThingWithALongName.SelfLink()
                            .SelfLink()
                            .SelectName(n => n.Name)
                            .WhereSomething(x => x.IsEnabledAndReady)
                        + otherFallbackValueName.SomeFallbackProperty;
                    var v = someParticularThingWithALongName.SelfLink()
                            .SelfLink()
                            .SelectName(n => n.Name)
                            .WhereSomething(x => x.IsEnabledAndReady)
                        ?? otherFallbackValueName.SomeFallbackProperty;
                    return someParticularThingWithALongName.SelfLink()
                            .SelfLink()
                            .SelectName(n => n.Name)
                            .WhereSomething(x => x.IsEnabledAndReady)
                        && otherFallbackValueName.SomeFallbackProperty;
                }
            }
            """
        );

    /// <summary>The control: the same chain as the right operand, conformant before and after.</summary>
    [Fact]
    public void AsTheRightOperand_TheChainNestsFromItsOwnLine() =>
        Oracle.Agrees(
            $$"""
              class T {
                  void M() {
                      var r = otherFallbackValueName.SomeFallbackProperty + {{Chain}};
                  }
              }
              """,
            """
            class T {
                void M() {
                    var r = otherFallbackValueName.SomeFallbackProperty
                        + someParticularThingWithALongName.SelfLink()
                            .SelfLink()
                            .SelectName(n => n.Name)
                            .WhereSomething(x => x.IsEnabledAndReady);
                }
            }
            """
        );
}
