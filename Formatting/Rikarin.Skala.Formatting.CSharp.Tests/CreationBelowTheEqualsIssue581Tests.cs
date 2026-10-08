using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #581, SK-DIV-0322: a creation with an initializer written on one line, after an <c>=</c> that does not
///     fit, moves down whole by a limit of its own, and its braces break past it. These are the boundary cells the
///     committed fixture (<c>constructs/wrapping/a-creation-moved-below-the-equals.cs</c>) cannot reach: another
///     block depth, a field, the twelve-column floor and an anonymous object's second pass. Every expected string is
///     <c>jb cleanupcode</c> 2025.2.6's own output, and each test asserts the second pass too.
/// </summary>
public sealed class CreationBelowTheEqualsIssue581Tests {
    static void Agrees(string source, string expected) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        var once = CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
        Assert.True(
            expected.TrimEnd('\n') == once.TrimEnd('\n'),
            $"Skala's output is not the oracle's.\n--- Skala ---\n{once}\n--- oracle ---\n{expected}"
        );

        var twice = CSharpFormatter.Format("Test.cs", SourceText.From(once), options).Formatted;
        Assert.True(once == twice, $"took two passes to settle:\n{once}\n--- pass two ---\n{twice}");
    }

    /// <summary>Two blocks deeper, the limit is one column lower: 108 moves down, 109 breaks the braces.</summary>
    [Fact]
    public void TwoBlocksDeeper_TheLimitIsOneColumnLower() {
        Agrees(
            """
            class C {
                void M() {
                    {
                        {
                            var vxxxxxxxxxxxxxxxxxxxxxxx = new Something { Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeee };
                        }
                    }
                }
            }
            """,
            """
            class C {
                void M() {
                    {
                        {
                            var vxxxxxxxxxxxxxxxxxxxxxxx =
                                new Something { Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeee };
                        }
                    }
                }
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    {
                        {
                            var vxxxxxxxxxxxxxxxxxxxxxxx = new Something { Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee };
                        }
                    }
                }
            }
            """,
            """
            class C {
                void M() {
                    {
                        {
                            var vxxxxxxxxxxxxxxxxxxxxxxx = new Something {
                                Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee
                            };
                        }
                    }
                }
            }
            """
        );
    }

    /// <summary>A field: 106 moves down, 107 breaks the braces.</summary>
    [Fact]
    public void AField_HasItsOwnLimit() {
        Agrees(
            """
            class C {
                private Something vxxxxxxxxxxxxxxxxxxxxxxxxxxx = new Something { Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee };
            }
            """,
            """
            class C {
                private Something vxxxxxxxxxxxxxxxxxxxxxxxxxxx =
                    new Something { Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee };
            }
            """
        );
        Agrees(
            """
            class C {
                private Something vxxxxxxxxxxxxxxxxxxxxxxxxxxx = new Something { Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee };
            }
            """,
            """
            class C {
                private Something vxxxxxxxxxxxxxxxxxxxxxxxxxxx = new Something {
                    Alpha = alphaaaaaaaaaaaaaaaa, Beta = beeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee
                };
            }
            """
        );
    }

    /// <summary>Under a twelve-column head the braces break whatever the width; at twelve the same value moves down.</summary>
    [Fact]
    public void UnderTwelveColumnsOfHead_TheBracesBreak() {
        Agrees(
            """
            class C {
                void M() {
                    var vxxxx = new SomeTypeWithALongName { FirstPropertyName = 1, SecondPropertyName = 2, ThirdPropertyName = 333 };
                }
            }
            """,
            """
            class C {
                void M() {
                    var vxxxx = new SomeTypeWithALongName {
                        FirstPropertyName = 1, SecondPropertyName = 2, ThirdPropertyName = 333
                    };
                }
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    var vxxxxx = new SomeTypeWithALongName { FirstPropertyName = 1, SecondPropertyName = 2, ThirdPropertyName = 333 };
                }
            }
            """,
            """
            class C {
                void M() {
                    var vxxxxx =
                        new SomeTypeWithALongName { FirstPropertyName = 1, SecondPropertyName = 2, ThirdPropertyName = 333 };
                }
            }
            """
        );
    }

    /// <summary>An anonymous object the author broke after its <c>{</c> keeps the break, as SK-DIV-0337's creations do.</summary>
    [Fact]
    public void AnAnonymousObjectBrokenAfterItsBrace_KeepsTheBreak() {
        Agrees(
            """
            class C {
                void M() {
                    var vxxxxxxxxxxxxxxx = new {
                        A = 1, B = "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
                    };
                }
            }
            """,
            """
            class C {
                void M() {
                    var vxxxxxxxxxxxxxxx = new {
                        A = 1, B = "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
                    };
                }
            }
            """
        );
    }
}
