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

    /// <summary>
    ///     #623, fuzz 13197324971917557925: the brace keeps its break only while the line through it fits. Behind a
    ///     head that leaves no room for <c>= new T() {</c> the oracle breaks the <c>=</c> and joins the braces; Skala
    ///     kept the <c>=</c>, broke after <c>new Memory&lt;</c>, and pass two broke the <c>=</c>. Expected output is
    ///     the oracle's, measured 2026-10-14 with <c>Testing ask</c>.
    /// </summary>
    [Fact]
    public void ABraceWhoseLineOverflows_BreaksTheEquals() =>
        Oracle.Agrees(
            """
            public class C {
                public ((DateTime First, Span<string> Second) First, Lazy<Dictionary<Guid, List<Guid>>> Second) P2 { get; } = new Memory<decimal>() {
            P18 = new { Id = @"verbatim\path", Count = "sssssssssssssssss" }, P19 = (1.5d - true) };
                public ((DateTime First, Span<string> Second) First, Lazy<Dictionary<Guid, List<Guid>>> Second) P4 { get; } = new Memory() {
            P18 = new {
            Id = @"verbatim\path", Count = "sssssssssssssssss" }, P19 = (1.5d - true) };
                public ((DateTime First, Span<string> Second) First, Lazy<Dictionary<Guid, List<Guid>>> Second) F5 = new Memory<decimal>() {
            P18 = new {
            Id = @"verbatim\path", Count = "sssssssssssssssss" }, P19 = (1.5d - true) };
                void M2() {
                    var vxxxxxxxxxx = new Something {
            Alpha = someLongerValueName, Beta = anotherLongerValueName, Gamma = "sssssssssssssssss" };
                }
                void M11() {
                    var vxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx = new Something {
            Alpha = 1, Beta = 2 };
                }
            }
            """,
            """
            public class C {
                public ((DateTime First, Span<string> Second) First, Lazy<Dictionary<Guid, List<Guid>>> Second) P2 { get; } =
                    new Memory<decimal>() { P18 = new { Id = @"verbatim\path", Count = "sssssssssssssssss" }, P19 = (1.5d - true) };

                public ((DateTime First, Span<string> Second) First, Lazy<Dictionary<Guid, List<Guid>>> Second) P4 { get; } =
                    new Memory() { P18 = new { Id = @"verbatim\path", Count = "sssssssssssssssss" }, P19 = (1.5d - true) };

                public ((DateTime First, Span<string> Second) First, Lazy<Dictionary<Guid, List<Guid>>> Second) F5 =
                    new Memory<decimal>() { P18 = new { Id = @"verbatim\path", Count = "sssssssssssssssss" }, P19 = (1.5d - true) };

                void M2() {
                    var vxxxxxxxxxx = new Something {
                        Alpha = someLongerValueName, Beta = anotherLongerValueName, Gamma = "sssssssssssssssss"
                    };
                }

                void M11() {
                    var vxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx =
                        new Something { Alpha = 1, Beta = 2 };
                }
            }
            """
        );

    /// <summary>
    ///     And a creation written on one line (fuzz 9076957179953969410, found by #623's own fuzz): #581's limit kept the
    ///     <c>=</c> with <c>= new {</c> past the margin, and the brace's break then made it the case above. Rows where the
    ///     line through the <c>{</c> ends past the margin, a field under a tuple type, a local and a field; expected output
    ///     is the oracle's, measured 2026-10-14 with <c>Testing ask</c>.
    /// </summary>
    [Fact]
    public void AOneLineCreationWhoseBraceOverflows_BreaksTheEquals() =>
        Oracle.Agrees(
            """"
            public struct T51<T52> where T52 : IDisposable, new() {
                public class T75<T76, T77> : IDisposable where T77 : class, new() {
                    private readonly (Lazy<long> First, Span<Dictionary<CancellationToken, List<CancellationToken>>> Second) f78 = new { Name = new string() { P79 = 3_000_000L, P80 = "ss", P81 = 1.0m }, Count = """<x a="1"/>""" };
                }
            }
            public class C {
                void M12() {
                    var M12xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx = new Something { Alpha = someLongerValueName, Beta = anotherLongerValueName, Gamma = "sssssssssssssssss" };
                }

                private readonly Dictionary<string, int> M32xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx = new Something { Alpha = someLongerValueName, Beta = anotherLongerValueName, Gamma = "sssssssssssssssss" };
            }
            """",
            """"
            public struct T51<T52> where T52 : IDisposable, new() {
                public class T75<T76, T77> : IDisposable where T77 : class, new() {
                    private readonly (Lazy<long> First, Span<Dictionary<CancellationToken, List<CancellationToken>>> Second) f78 =
                        new { Name = new string() { P79 = 3_000_000L, P80 = "ss", P81 = 1.0m }, Count = """<x a="1"/>""" };
                }
            }

            public class C {
                void M12() {
                    var M12xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx =
                        new Something { Alpha = someLongerValueName, Beta = anotherLongerValueName, Gamma = "sssssssssssssssss" };
                }

                private readonly Dictionary<string, int> M32xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx =
                    new Something { Alpha = someLongerValueName, Beta = anotherLongerValueName, Gamma = "sssssssssssssssss" };
            }
            """"
        );
}
