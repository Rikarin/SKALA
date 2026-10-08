using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;
using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #537: an attribute section of several attributes keeps a certain item's head on its line. Every expected
///     string is <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class AttributeSectionHeadIssue537Tests {
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
    ///     #537: an attribute in a section of several whose arguments are certain to chop stays on the section&apos;s
    ///     line; one merely too wide moves below.
    /// </summary>
    [Fact]
    public void ACertainAttribute_KeepsItsHeadBesideTheOthers() {
        Agrees(
            """
            class C {
                void M([Obsolete, Description("aaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbb")] int a) { }
            }
            """,
            """
            class C {
                void M(
                    [Obsolete, Description(
                         "aaaaaaaaaaaaaaaaaaaaaaa",
                         "bbbbbbbbbbbbbbbbb"
                     )]
                    int a
                ) { }
            }
            """
        );
        Agrees(
            $$"""
              class C {
                  void M(int b, [Obsolete, Description("{{R('a', 110)}}")] int a) { }
              }
              """,
            $$"""
              class C {
                  void M(
                      int b,
                      [Obsolete,
                       Description(
                           "{{R('a', 110)}}"
                       )]
                      int a
                  ) { }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  void M(int b, [Obsolete, Description("{{R('a', 80)}}")] int a) { }
              }
              """,
            $$"""
              class C {
                  void M(
                      int b,
                      [Obsolete, Description("{{R('a', 80)}}")]
                      int a
                  ) { }
              }
              """
        );
        Agrees(
            """
            class C {
                [Obsolete, Description("aaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbb")]
                void M() { }
            }
            """,
            """
            class C {
                [Obsolete, Description(
                     "aaaaaaaaaaaaaaaaaaaaaaa",
                     "bbbbbbbbbbbbbbbbb"
                 )]
                void M() { }
            }
            """
        );
        Agrees(
            """
            class C {
                void M([Obsolete, Serializable, Description("aaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbb")] int a) { }
            }
            """,
            """
            class C {
                void M(
                    [Obsolete, Serializable, Description(
                         "aaaaaaaaaaaaaaaaaaaaaaa",
                         "bbbbbbbbbbbbbbbbb"
                     )]
                    int a
                ) { }
            }
            """
        );
        Agrees(
            """
            class C {
                void M([Description("aaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbb"), Obsolete] int a) { }
            }
            """,
            """
            class C {
                void M(
                    [Description(
                         "aaaaaaaaaaaaaaaaaaaaaaa",
                         "bbbbbbbbbbbbbbbbb"
                     ), Obsolete]
                    int a
                ) { }
            }
            """
        );
    }
}
