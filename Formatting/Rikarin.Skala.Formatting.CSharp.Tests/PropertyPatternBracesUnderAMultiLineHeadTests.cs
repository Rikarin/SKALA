using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A property pattern's braces break open whenever the pattern they belong to already spans lines at its <c>{</c> —
///     a type argument list filled or kept broken, a positional clause, a subpattern's colon broken (fuzz
///     1044487512275255493).
/// </summary>
/// <remarks>
///     ⚠ Skala opened them only for a colon break the author kept (#561), a source fact: pass one added the colon's
///     break and filled the type arguments with the braces joined, and pass two, reading its own breaks as the
///     author's, broke them. Expected outputs are the oracle's, measured 2026-11-11 with <c>Testing ask</c>, except
///     the first: on a flat input the oracle keeps <c>P146: Dictionary&lt;</c> glued where Skala breaks the colon (a
///     separate, recorded divergence), so that test pins the layout Skala reaches, which the oracle leaves unchanged.
/// </remarks>
public sealed class PropertyPatternBracesUnderAMultiLineHeadTests {
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    static void Check(string source, string oracle) {
        var first = FormatWith(source);
        Assert.Equal(oracle, first);
        Assert.Equal(first, FormatWith(first));
    }

    [Fact]
    public void ATypeArgumentListTheValueFilled() =>
        Check(
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                case bool { P146: Dictionary<TimeSpan_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww, List<TimeSpan_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww>> { P149: not null } }:
                                    break;
                            }
                        }
                    }
                }
            }

            """,
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                case bool {
                                    P146:
                                    Dictionary<TimeSpan_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww,
                                        List<TimeSpan_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww>> {
                                        P149: not null
                                    }
                                }:
                                    break;
                            }
                        }
                    }
                }
            }

            """
        );

    [Fact]
    public void ATypeArgumentListTheAuthorBroke() =>
        Check(
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                case bool {
                                    P146: Dictionary<TimeSpan_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww,
                                        List<TimeSpan_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww>> { P149: not null }
                                }:
                                    break;
                            }
                        }
                    }
                }
            }

            """,
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                case bool {
                                    P146: Dictionary<TimeSpan_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww,
                                        List<TimeSpan_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww>> {
                                        P149: not null
                                    }
                                }:
                                    break;
                            }
                        }
                    }
                }
            }

            """
        );

    [Fact]
    public void AColonBreakTheGroupAdded() =>
        Check(
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                case bool { PropertyNameThatIsQuiteLongIndeedForTheProbeAndThenSomeMoreXx: SomeTypeNameOfFortyCharactersWideXxxxxxx { P149: not null } }:
                                    break;
                            }
                        }
                    }
                }
            }

            """,
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                case bool {
                                    PropertyNameThatIsQuiteLongIndeedForTheProbeAndThenSomeMoreXx:
                                    SomeTypeNameOfFortyCharactersWideXxxxxxx {
                                        P149: not null
                                    }
                                }:
                                    break;
                            }
                        }
                    }
                }
            }

            """
        );

    [Fact]
    public void AColonBreakTheAuthorKept() =>
        Check(
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                case bool { P146:
                                    Dictionary<int, int> { P149: not null } }:
                                    break;
                            }
                        }
                    }
                }
            }

            """,
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                case bool {
                                    P146:
                                    Dictionary<int, int> {
                                        P149: not null
                                    }
                                }:
                                    break;
                            }
                        }
                    }
                }
            }

            """
        );

    [Fact]
    public void ACaseLabelsOwnType() =>
        Check(
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                case Dictionary<TimeSpan_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww, List<TimeSpan_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww>> { P149: not null }:
                                    break;
                            }
                        }
                    }
                }
            }

            """,
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                case Dictionary<TimeSpan_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww,
                                    List<TimeSpan_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww>> {
                                    P149: not null
                                }:
                                    break;
                            }
                        }
                    }
                }
            }

            """
        );

    [Fact]
    public void BeforeADesignation() =>
        Check(
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                case bool { P146: Foo<int,
                                        int> { P149: not null } x }:
                                    break;
                            }
                        }
                    }
                }
            }

            """,
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                case bool {
                                    P146: Foo<int,
                                        int> {
                                        P149: not null
                                    } x
                                }:
                                    break;
                            }
                        }
                    }
                }
            }

            """
        );

    [Fact]
    public void ASwitchArmsType() =>
        Check(
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                default:
                                    var r = o switch {
                                        Dictionary<int,
                                            int> { P149: not null } => 1,
                                        _ => 2
                                    };
                                    break;
                            }
                        }
                    }
                }
            }

            """,
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                default:
                                    var r = o switch {
                                        Dictionary<int,
                                            int> {
                                            P149: not null
                                        } => 1,
                                        _ => 2
                                    };
                                    break;
                            }
                        }
                    }
                }
            }

            """
        );

    [Fact]
    public void BehindNot() =>
        Check(
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                case bool { P146: not Foo<int,
                                        int> { P149: not null } }:
                                    break;
                            }
                        }
                    }
                }
            }

            """,
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                case bool {
                                    P146: not Foo<int,
                                        int> {
                                        P149: not null
                                    }
                                }:
                                    break;
                            }
                        }
                    }
                }
            }

            """
        );

    [Fact]
    public void BeforeAPositionalClause() =>
        Check(
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                case bool { P146: Foo<int,
                                        int>(1, 2) { P149: not null } }:
                                    break;
                            }
                        }
                    }
                }
            }

            """,
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                case bool {
                                    P146: Foo<int,
                                        int>(1, 2) {
                                        P149: not null
                                    }
                                }:
                                    break;
                            }
                        }
                    }
                }
            }

            """
        );

    [Fact]
    public void NotAColonBreakBehindNot() =>
        Check(
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                case bool { LongName:
                                    not Foo { P149: not null } }:
                                    break;
                            }
                        }
                    }
                }
            }

            """,
            """
            public sealed class T1 {
                public sealed class T2 {
                    internal void M22(object o) {
                        try { } catch (IOException exception56) {
                            switch (o) {
                                case bool {
                                    LongName:
                                    not Foo { P149: not null }
                                }:
                                    break;
                            }
                        }
                    }
                }
            }

            """
        );
}
