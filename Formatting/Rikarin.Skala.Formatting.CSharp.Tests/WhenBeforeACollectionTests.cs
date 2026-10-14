namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Fuzz 16565442098129427039: a break the author kept between an arm's <c>when</c> and a collection expression stays,
///     with the braces before it lifted as for any kept break after the <c>when</c>. The expected string is
///     <c>jb cleanupcode</c>'s own output, measured 2026-10-14 with <c>Testing ask</c>.
/// </summary>
public sealed class WhenBeforeACollectionTests {
    /// <summary>Before the fix both joined <c>when [</c>, and M37's braces moved between the two passes.</summary>
    [Fact]
    public void ABreakBeforeACollectionAfterWhen_IsKept() =>
        Oracle.Agrees(
            """
            class T12 {
                public void M37() {
                    var v50 = value switch { { Value.Length:
            > 2, Kind: null } when
            ["ssss", null] => default, _ => "ssssssssssssssssss" };
                }

                public void M38() {
                    var v50 = value switch { { Value.Length: > 2, Kind: null } when
            ["ssss", null] => default, _ => "ssssssssssssssssss" };
                }
            }
            """,
            """
            class T12 {
                public void M37() {
                    var v50 = value switch {
                        {
                                Value.Length:
                                > 2,
                                Kind: null
                            } when
                            ["ssss", null] => default,
                        _ => "ssssssssssssssssss"
                    };
                }

                public void M38() {
                    var v50 = value switch {
                        { Value.Length: > 2, Kind: null } when
                            ["ssss", null] => default,
                        _ => "ssssssssssssssssss"
                    };
                }
            }
            """
        );
}
