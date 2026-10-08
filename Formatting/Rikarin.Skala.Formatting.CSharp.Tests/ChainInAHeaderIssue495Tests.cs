namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #495: a chain that is a statement's whole condition, or the body of a call's sole lambda
///     argument, shares the level already spent around it instead of adding its own. Every expected
///     string is <c>jb cleanupcode</c>'s own output, measured 2026-10-08 with <c>Testing ask</c>;
///     <c>constructs/breaks/chain-in-a-header-or-a-sole-lambda.cs</c> holds the wider set and its controls.
/// </summary>
public sealed class ChainInAHeaderIssue495Tests {
    /// <summary>Before the fix: <c>.Any(predicateValue)) {</c> at 16 and 19, a level past the aligned column.</summary>
    [Fact]
    public void AWholeCondition_PutsTheDotsOnTheAlignedColumn() =>
        Oracle.Agrees(
            """
            class T {
                void M() {
                    if (source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThreeeeee).Any(predicateValue)) {
                        A();
                    }

                    while (source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThreeeee).Any(predicateValue)) {
                        A();
                    }

                    if (!source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThreeeeee).Any(predicateValue)) {
                        A();
                    }
                }
            }
            """,
            """
            class T {
                void M() {
                    if (source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThreeeeee)
                        .Any(predicateValue)) {
                        A();
                    }

                    while (source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThreeeee)
                           .Any(predicateValue)) {
                        A();
                    }

                    if (!source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThreeeeee)
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
            """
            class T {
                void M() {
                    Use(x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaaaaaaaaaa).Where(predicateValue));
                    Use(first, x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaaa).Where(predicateValue));
                }
            }
            """,
            """
            class T {
                void M() {
                    Use(x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaaaaaaaaaa)
                        .Where(predicateValue)
                    );
                    Use(
                        first,
                        x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaaa)
                            .Where(predicateValue)
                    );
                }
            }
            """
        );
}
