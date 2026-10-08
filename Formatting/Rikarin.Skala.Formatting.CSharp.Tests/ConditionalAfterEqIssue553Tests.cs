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

    /// <summary>
    ///     A condition the author already broke at its operators keeps the <c>=</c>: the oracle leaves
    ///     both rows as written, where the same conditions written on one line break the <c>=</c>. Found on
    ///     Skala's own source (<c>StaticMemberViaDerivedTypeAnalyzer</c>, <c>LargeStructArgumentAnalyzer</c>),
    ///     which the first cut of the rule moved.
    /// </summary>
    [Fact]
    public void AConditionAlreadyBrokenAtItsOperators_KeepsTheEquals() =>
        Oracle.Agrees(
            """
            class T {
                object M() {
                    var properties = replacement is null
                        || RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(access.SyntaxTree, span)
                            ? null
                            : FixEdits.Pack((span, replacement));
                    var threshold = options.TryGetValue("dotnet_code_quality.SK4007.threshold", out var configured)
                        && int.TryParse(configured, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                        && parsed > 0
                            ? parsed
                            : 64;
                    return null;
                }
            }
            """,
            """
            class T {
                object M() {
                    var properties = replacement is null
                        || RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(access.SyntaxTree, span)
                            ? null
                            : FixEdits.Pack((span, replacement));
                    var threshold = options.TryGetValue("dotnet_code_quality.SK4007.threshold", out var configured)
                        && int.TryParse(configured, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                        && parsed > 0
                            ? parsed
                            : 64;
                    return null;
                }
            }
            """
        );
}
