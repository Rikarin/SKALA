namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Round four's author-layout survey: an <c>=</c> before a creation whose initializer the author broke
///     after its <c>{</c> keeps the brace's break where the statement does not fit on one line, even where
///     the same initializer written flat moves down whole after the <c>=</c>. Every expected string is
///     <c>jb cleanupcode</c>'s own output, measured 2026-10-09 with <c>Testing ask</c>.
/// </summary>
public sealed class InitializerBrokenAfterBraceTests {
    /// <summary>
    ///     Before the fix every row broke the <c>=</c> and joined the braces. The fourth row, which fits on
    ///     one line, is joined by both engines; the last, braced on a line of its own (Newtonsoft's style), is not
    ///     the author's brace break and moves down whole after the <c>=</c>.
    /// </summary>
    [Fact]
    public void AnInitializerTheAuthorBroke_KeepsItsBrace() =>
        Oracle.Agrees(
            """
            class T {
                object M() {
                    var someVeryVeryVeryVeryVeryVeryLongName = new Something {
                        Alpha = alphaaaaaaaaaaaaaaaa, Beta = betaaaaaaaaaaaaaa
                    };
                    var someVeryVeryVeryVeryVeryVeryLongName = new Something {
                        Alpha = alphaaaaaaaaaaaaaaaa,
                        Beta = betaaaaaaaaaaaaaa
                    };
                    someVeryVeryVeryVeryVeryVeryLongTarget = new Something {
                        Alpha = alphaaaaaaaaaaaaaaaa, Beta = betaaaaaaaaaaaaaa
                    };
                    var shortName = new Something {
                        Alpha = alphaaaaaaaaaaaaaaaa, Beta = betaaaaaaaaaaaaaa
                    };
                    var someVeryVeryVeryVeryVeryVeryLongName = new Something
                    {
                        Alpha = alphaaaaaaaaaaaaaaaa, Beta = betaaaaaaaaaaaaaa
                    };
                    return null;
                }
            }
            """,
            """
            class T {
                object M() {
                    var someVeryVeryVeryVeryVeryVeryLongName = new Something {
                        Alpha = alphaaaaaaaaaaaaaaaa, Beta = betaaaaaaaaaaaaaa
                    };
                    var someVeryVeryVeryVeryVeryVeryLongName = new Something {
                        Alpha = alphaaaaaaaaaaaaaaaa, Beta = betaaaaaaaaaaaaaa
                    };
                    someVeryVeryVeryVeryVeryVeryLongTarget = new Something {
                        Alpha = alphaaaaaaaaaaaaaaaa, Beta = betaaaaaaaaaaaaaa
                    };
                    var shortName = new Something { Alpha = alphaaaaaaaaaaaaaaaa, Beta = betaaaaaaaaaaaaaa };
                    var someVeryVeryVeryVeryVeryVeryLongName =
                        new Something { Alpha = alphaaaaaaaaaaaaaaaa, Beta = betaaaaaaaaaaaaaa };
                    return null;
                }
            }
            """
        );
}
