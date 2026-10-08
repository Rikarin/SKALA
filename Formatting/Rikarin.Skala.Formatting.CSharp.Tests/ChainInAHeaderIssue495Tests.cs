namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #495: a chain that is a statement's whole condition, or the body of a call's sole lambda
///     argument, shares the level already spent around it instead of adding its own. Every expected
///     string is <c>jb cleanupcode</c>'s own output, measured 2026-10-08 with <c>Testing ask</c>;
///     <c>constructs/breaks/chain-in-a-header-or-a-sole-lambda.cs</c> holds the wider set and its controls.
/// </summary>
public sealed class ChainInAHeaderIssue495Tests {
    const string Long9 = "Use(x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, "
        + "gamxaaaaaaaaaaaaaaaaaaaaaaaaaaa)";

    const string Long10 = "x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamx"
        + "aaaaaaaaaaaaaaaaaaaa)";

    const string Long1 = "if (source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamma"
        + "ArgumentValueNumberThreeeeee).Any(predicateValue)) {";

    const string Long2 = "while (source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, ga"
        + "mmaArgumentValueNumberThreeeee).Any(predicateValue)) {";

    const string Long3 = "if (!source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamm"
        + "aArgumentValueNumberThreeeeee).Any(predicateValue)) {";

    const string Long4 = "if (source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamma"
        + "ArgumentValueNumberThreeeeee)";

    const string Long5 = "while (source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, ga"
        + "mmaArgumentValueNumberThreeeee)";

    const string Long6 = "if (!source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamm"
        + "aArgumentValueNumberThreeeeee)";

    const string Long7 = "Use(x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, "
        + "gamxaaaaaaaaaaaaaaaaaaaaaaaaaaa).Where(predicateValue));";

    const string Long8 = "Use(first, x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumb"
        + "erTwo, gamxaaaaaaaaaaaaaaaaaaaa).Where(predicateValue));";

    /// <summary>Before the fix: <c>.Any(predicateValue)) {</c> at 16 and 19, a level past the aligned column.</summary>
    [Fact]
    public void AWholeCondition_PutsTheDotsOnTheAlignedColumn() =>
        Oracle.Agrees(
            $$"""
              class T {
                  void M() {
                      {{Long1}}
                          A();
                      }

                      {{Long2}}
                          A();
                      }

                      {{Long3}}
                          A();
                      }
                  }
              }
              """,
            $$"""
              class T {
                  void M() {
                      {{Long4}}
                          .Any(predicateValue)) {
                          A();
                      }

                      {{Long5}}
                             .Any(predicateValue)) {
                          A();
                      }

                      {{Long6}}
                              .Any(predicateValue)) {
                          A();
                      }
                  }
              }
              """
        );

    /// <summary>
    ///     Before the fix: <c>.Where(predicateValue)</c> at 16. The lambda after another argument is the
    ///     control, one level past its own line before and after.
    /// </summary>
    [Fact]
    public void ASoleLambdasBody_PutsTheDotsOneLevelPastTheStatement() =>
        Oracle.Agrees(
            $$"""
              class T {
                  void M() {
                      {{Long7}}
                      {{Long8}}
                  }
              }
              """,
            $$"""
              class T {
                  void M() {
                      {{Long9}}
                          .Where(predicateValue)
                      );
                      Use(
                          first,
                          {{Long10}}
                              .Where(predicateValue)
                      );
                  }
              }
              """
        );
}
