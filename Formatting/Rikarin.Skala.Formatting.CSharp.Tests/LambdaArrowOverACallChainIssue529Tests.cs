namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #529: a lambda whose body is a chain of calls breaks its arrow exactly when the whole chain
///     then fits on the line below. Every expected string is <c>jb cleanupcode</c>'s own output, measured
///     2026-10-08 with <c>Testing ask</c>; <c>constructs/wrapping/lambda-arrow-over-a-call-chain.cs</c> holds
///     the wider set.
/// </summary>
public sealed class LambdaArrowOverACallChainIssue529Tests {
    /// <summary>
    ///     Before the fix: <c>items.Where(x =&gt; source.Select(…)</c> / <c>.Any(predicateValue)</c>. The
    ///     control keeps the arrow because the chain does not fit below either.
    /// </summary>
    [Fact]
    public void AChainThatFitsBelow_MovesDownWhole() =>
        Oracle.Agrees(
            """
            class T {
                void N() {
                    var r = items.Where(x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaa).Any(predicateValue));
                    Use(x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaaaaaaaaaa).Where(predicateValue));
                }
            }
            """,
            """
            class T {
                void N() {
                    var r = items.Where(x =>
                        source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaa).Any(predicateValue)
                    );
                    Use(x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaaaaaaaaaa)
                        .Where(predicateValue)
                    );
                }
            }
            """
        );
}
