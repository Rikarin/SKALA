namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A break the author kept after a positional pattern's <c>(</c> before a property pattern's <c>{</c> is
///     the parenthesis's, not the brace's: it stays (fuzz 11693758747470537505).
/// </summary>
/// <remarks>
///     ⚠ The expected string is the oracle's answer to this input, asked 2026-10-09.
/// </remarks>
public sealed class PositionalBraceAfterParenthesisTests {
    [Fact]
    public void ABreakBeforeTheFirstElementsBrace_IsKept() =>
        Oracle.Agrees(
            """
            class C {
                public decimal P37 {
                    get => state is (
            { Length: > 0 }, ImmutableArray<object?> typed56);
                }

                void M() {
                    var x = state is (
            { Length: > 0 }, int y);
                    var z = state is (
                        Foo { Length: > 0 }, int w);
                }
            }
            """,
            """
            class C {
                public decimal P37 {
                    get =>
                        state is (
                            { Length: > 0 }, ImmutableArray<object?> typed56);
                }

                void M() {
                    var x = state is (
                        { Length: > 0 }, int y);
                    var z = state is (
                        Foo { Length: > 0 }, int w);
                }
            }
            """
        );
}
