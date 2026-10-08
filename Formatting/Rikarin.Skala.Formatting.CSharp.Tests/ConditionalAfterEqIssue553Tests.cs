namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #553: an <c>=</c> whose value is a conditional breaks when the condition does not fit beside
///     it and the head is twelve columns or more. Every expected string is <c>jb cleanupcode</c>'s own
///     output, measured 2026-10-09 with <c>Testing ask</c>; <c>constructs/breaks/conditional-after-eq.cs</c>
///     holds the wider set.
/// </summary>
public sealed class ConditionalAfterEqIssue553Tests {
    /// <summary>
    ///     Before the fix the first row kept the <c>=</c> and chopped the chain. The second, a short head,
    ///     is the control.
    /// </summary>
    [Fact]
    public void AConditionThatDoesNotFitBesideAWideHead_BreaksTheEquals() =>
        Oracle.Agrees(
            """
            class T {
                object M() {
                    var a7aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = someParticularThingWithALongName.SelfLink().SelfLink().SelectName(n => n.Name) ? otherFallbackValueName.SomeFallbackProperty : third;
                    var va = someParticularThingWithALongName.SelfLink().SelfLink().SelectName(n => n.Name).WhereSomething(x => x.IsEnabledAndReady) ? otherFallbackValueName.SomeFallbackProperty : third;
                    return null;
                }
            }
            """,
            """
            class T {
                object M() {
                    var a7aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa =
                        someParticularThingWithALongName.SelfLink().SelfLink().SelectName(n => n.Name)
                            ? otherFallbackValueName.SomeFallbackProperty
                            : third;
                    var va = someParticularThingWithALongName.SelfLink()
                        .SelfLink()
                        .SelectName(n => n.Name)
                        .WhereSomething(x => x.IsEnabledAndReady)
                        ? otherFallbackValueName.SomeFallbackProperty
                        : third;
                    return null;
                }
            }
            """
        );
}
