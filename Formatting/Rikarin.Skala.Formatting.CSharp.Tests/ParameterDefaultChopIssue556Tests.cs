using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #556, SK-DIV-0352: a short parameter&apos;s default behind its attribute section. Every expected string is <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class ParameterDefaultChopIssue556Tests {
    /// <summary>The oracle's answer under the repository's export with <paramref name="overrides" /> on top.</summary>
    static void Agrees(string source, string expected, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [.. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
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

    /// <summary>#556: int a = 5 behind its one attribute section chops the arguments when the joined line overflows, as int a does.</summary>
    [Fact]
    public void AShortParameterWithADefault_ChopsTheSectionToo() {
        Agrees(
            """
            class C {
                void M(int b, [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] int a = 5) { }
            }
            """,
            """
            class C {
                void M(
                    int b,
                    [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] int a = 5
                ) { }
            }
            """
        );
        Agrees(
            """
            class C {
                void M(int b, [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] int a = 5) { }
            }
            """,
            """
            class C {
                void M(
                    int b,
                    [Obsolete(
                        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                        true
                    )]
                    int a = 5
                ) { }
            }
            """
        );
        Agrees(
            """
            class C {
                void M(int b, [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] int a = 5) { }
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
                    int a = 5
                ) { }
            }
            """
        );
        Agrees(
            """
            class C {
                void M(int b, [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] int a = 5) { }
            }
            """,
            """
            class C {
                void M(
                    int b,
                    [Obsolete(
                        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                        true
                    )]
                    int a = 5
                ) { }
            }
            """
        );
    }
}
