using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #476, SK-DIV-0352: a parameter&apos;s one attribute section and when its arguments chop. Every expected
///     string is <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class AttributeArgumentChopIssue476Tests {
    /// <summary>The oracle's answer under the repository's export with <paramref name="overrides" /> on top.</summary>
    static void Agrees(string source, string expected, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                    Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                    [..overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
                )
                .Options
        );

        var once = CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
        Assert.True(
            expected.TrimEnd('\n') == once.TrimEnd('\n'),
            $"Skala's output is not the oracle's.\n--- Skala ---\n{once}\n--- oracle ---\n{expected}"
        );

        var twice = CSharpFormatter.Format("Test.cs", SourceText.From(once), options).Formatted;
        Assert.True(once == twice, $"took two passes to settle:\n{once}\n--- pass two ---\n{twice}");
    }

    /// <summary>
    ///     #476: behind one attribute section a parameter of at most eleven columns puts the section&apos;s arguments in
    ///     a chop exactly when the joined line overflows; the oracle never stands it alone below a whole section.
    /// </summary>
    [Fact]
    public void AShortParameter_ChopsTheSectionWhenTheJoinedLineOverflows() {
        Agrees(
            """
            class C {
                void M(int b, [A("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")] int a) { }
            }
            """,
            """
            class C {
                void M(
                    int b,
                    [A("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")] int a
                ) { }
            }
            """
        );
        Agrees(
            """
            class C {
                void M(int b, [A("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")] int a) { }
            }
            """,
            """
            class C {
                void M(
                    int b,
                    [A(
                        "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
                    )]
                    int a
                ) { }
            }
            """
        );
        Agrees(
            """
            class C {
                void M(int b, [A("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")] int a) { }
            }
            """,
            """
            class C {
                void M(
                    int b,
                    [A(
                        "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
                    )]
                    int a
                ) { }
            }
            """
        );
        Agrees(
            """
            class C {
                void M(int b, [A("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", 1)] string a) { }
            }
            """,
            """
            class C {
                void M(
                    int b,
                    [A("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", 1)] string a
                ) { }
            }
            """
        );
        Agrees(
            """
            class C {
                void M(int b, [A("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", 1)] string a) { }
            }
            """,
            """
            class C {
                void M(
                    int b,
                    [A(
                        "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
                        1
                    )]
                    string a
                ) { }
            }
            """
        );
        Agrees(
            """
            class O {
                class C {
                    void M(int b, [Obsolete("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", true)] int a) { }
                }
            }
            """,
            """
            class O {
                class C {
                    void M(
                        int b,
                        [Obsolete("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", true)] int a
                    ) { }
                }
            }
            """
        );
        Agrees(
            """
            class O {
                class C {
                    void M(int b, [Obsolete("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", true)] int a) { }
                }
            }
            """,
            """
            class O {
                class C {
                    void M(
                        int b,
                        [Obsolete(
                            "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
                            true
                        )]
                        int a
                    ) { }
                }
            }
            """
        );
        Agrees(
            """
            class C {
                void M(int b, [A("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")] List<int> a) { }
            }
            """,
            """
            class C {
                void M(
                    int b,
                    [A("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")] List<int> a
                ) { }
            }
            """
        );
        Agrees(
            """
            class C {
                void M(int b, [A("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")] List<int> a) { }
            }
            """,
            """
            class C {
                void M(
                    int b,
                    [A(
                        "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
                    )]
                    List<int> a
                ) { }
            }
            """
        );
        Agrees(
            """
            class C {
                void M(int b, [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] string a) { }
            }
            """,
            """
            class C {
                void M(
                    int b,
                    [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] string a
                ) { }
            }
            """
        );
        Agrees(
            """
            class C {
                void M(int b, [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] string a) { }
            }
            """,
            """
            class C {
                void M(
                    int b,
                    [Obsolete(
                        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                        true
                    )]
                    string a
                ) { }
            }
            """
        );
        Agrees(
            """
            class C {
                void M(int b, [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] ref int a) { }
            }
            """,
            """
            class C {
                void M(
                    int b,
                    [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] ref int a
                ) { }
            }
            """
        );
        Agrees(
            """
            class C {
                void M(int b, [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] ref int a) { }
            }
            """,
            """
            class C {
                void M(
                    int b,
                    [Obsolete(
                        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                        true
                    )]
                    ref int a
                ) { }
            }
            """
        );
        Agrees(
            """
            class C {
                void M(int b, [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] List<int> a) { }
            }
            """,
            """
            class C {
                void M(
                    int b,
                    [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] List<int> a
                ) { }
            }
            """
        );
        Agrees(
            """
            class C {
                void M(int b, [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] List<int> a) { }
            }
            """,
            """
            class C {
                void M(
                    int b,
                    [Obsolete(
                        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                        true
                    )]
                    List<int> a
                ) { }
            }
            """
        );
        Agrees(
            """
            class C {
                void M(int b, [A("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")] int aaaaaaaaaaaaa) { }
            }
            """,
            """
            class C {
                void M(
                    int b,
                    [A("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
                    int aaaaaaaaaaaaa
                ) { }
            }
            """
        );
        Agrees(
            """
            class C {
                void M(int b, [Obsolete("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", true)] int aaaaaaaaaaaaaaaaaa) { }
            }
            """,
            """
            class C {
                void M(
                    int b,
                    [Obsolete("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", true)]
                    int aaaaaaaaaaaaaaaaaa
                ) { }
            }
            """
        );
    }
}
