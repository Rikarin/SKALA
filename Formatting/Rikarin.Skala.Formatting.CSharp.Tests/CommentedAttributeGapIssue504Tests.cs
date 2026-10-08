using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #504, SK-DIV-0201: a field&apos;s gap after its last attribute section and a block comment. Every expected
///     string is <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class CommentedAttributeGapIssue504Tests {
    const string Long1 = "[Obsolete] /* c */ public int F = alphaValue + betaValue + gammaValue + deltaVal"
        + "ue + epsilonValue + zzzzzzzzzzzzzzz;";

    const string Long2 = "[Obsolete] /* c */ public int F = alphaValue + betaValue + gammaValue + deltaVal"
        + "ue + epsilonValue + zzzzzzzzzzzzzzzz;";

    const string Long3 = "[Obsolete] /* c */ public int F = alphaValue + betaValue + gammaValue + deltaVal"
        + "ue + epsilonValue + zzzzzzzzzzzzzzzzzzzz;";

    const string Long4 = "[Obsolete] /* c */ public int F = Compute(alphaValue, betaValue, gammaValue, del"
        + "taValue, eeeeeeeeeeeeeeeeeeeeeeeeee);";

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
    ///     #504: a field with a block comment after its last attribute section is declined when the joined line
    ///     overflows, and a call or a creation only while its terminator alone overflows.
    /// </summary>
    [Fact]
    public void ACommentAfterTheAttribute_DeclinesTheJoinPastTheMargin() {
        Agrees(
            $$"""
              class C {
                  {{Long1}}
              }
              """,
            $$"""
              class C {
                  {{Long1}}
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{Long2}}
              }
              """,
            """
            class C {
                [Obsolete] /* c */
                public int F = alphaValue + betaValue + gammaValue + deltaValue + epsilonValue + zzzzzzzzzzzzzzzz;
            }
            """
        );
        Agrees(
            $$"""
              class C {
                  {{Long3}}
              }
              """,
            """
            class C {
                [Obsolete] /* c */
                public int F = alphaValue + betaValue + gammaValue + deltaValue + epsilonValue + zzzzzzzzzzzzzzzzzzzz;
            }
            """
        );
        Agrees(
            $$"""
              class C {
                  [Obsolete] /* c */ public int F = {{R('v', 82)}};
              }
              """,
            """
            class C {
                [Obsolete] /* c */
                public int F = vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv;
            }
            """
        );
        Agrees(
            $$"""
              class C {
                  [Obsolete] /* c */ public string F = "{{R('x', 78)}}";
              }
              """,
            """
            class C {
                [Obsolete] /* c */
                public string F = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
            }
            """
        );
        Agrees(
            $$"""
              class C {
                  {{Long4}}
              }
              """,
            """
            class C {
                [Obsolete] /* c */
                public int F = Compute(alphaValue, betaValue, gammaValue, deltaValue, eeeeeeeeeeeeeeeeeeeeeeeeee);
            }
            """
        );
        Agrees(
            $$"""
              class C {
                  [Obsolete] /* c */ public int F = alphaValue ? betaValue : gammaValue + {{R('z', 44)}};
              }
              """,
            """
            class C {
                [Obsolete] /* c */
                public int F = alphaValue ? betaValue : gammaValue + zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
            }
            """
        );
        Agrees(
            $$"""
              class C {
                  [Obsolete] /* c */ public int F = alphaValue ? betaValue : gammaValue + {{R('z', 57)}};
              }
              """,
            $$"""
              class C {
                  [Obsolete] /* c */
                  public int F = alphaValue ? betaValue : gammaValue + {{R('z', 57)}};
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  [Obsolete] /* c */ private static readonly int F = alphaValue + betaValue + {{R('z', 40)}};
              }
              """,
            """
            class C {
                [Obsolete] /* c */
                private static readonly int F = alphaValue + betaValue + zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
            }
            """
        );
        Agrees(
            $$"""
              class C {
                  [Obsolete] /* c */ private static readonly int F = alphaValue + betaValue + {{R('z', 53)}};
              }
              """,
            $$"""
              class C {
                  [Obsolete] /* c */
                  private static readonly int F = alphaValue + betaValue + {{R('z', 53)}};
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  [Obsolete] /* c */ public event System.EventHandler E{{R('z', 65)}};
              }
              """,
            """
            class C {
                [Obsolete] /* c */
                public event System.EventHandler Ezzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
            }
            """
        );
        Agrees(
            $$"""
              class C {
                  [Obsolete] /* c */ public int F = alpha.Beta.Gamma.Delta.Epsilon.Compute().Z{{R('z', 40)}};
              }
              """,
            """
            class C {
                [Obsolete] /* c */
                public int F = alpha.Beta.Gamma.Delta.Epsilon.Compute().Zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
            }
            """
        );
        Agrees(
            $$"""
              class C {
                  [Obsolete] /* c */ public int P { get; set; } = alphaValue + betaValue + {{R('z', 43)}};
              }
              """,
            """
            class C {
                [Obsolete] /* c */ public int P { get; set; } =
                    alphaValue + betaValue + zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
            }
            """
        );
    }

    /// <summary>#504 under the field and accessor-holder keys at always.</summary>
    [Fact]
    public void AtAlways_TheSame() {
        Agrees(
            $$"""
              class C {
                  {{Long1}}
              }
              """,
            $$"""
              class C {
                  {{Long1}}
              }
              """,
            ("skala_place_field_attribute_on_same_line", "always"),
            ("skala_place_accessorholder_attribute_on_same_line", "always")
        );
        Agrees(
            $$"""
              class C {
                  {{Long2}}
              }
              """,
            """
            class C {
                [Obsolete] /* c */
                public int F = alphaValue + betaValue + gammaValue + deltaValue + epsilonValue + zzzzzzzzzzzzzzzz;
            }
            """,
            ("skala_place_field_attribute_on_same_line", "always"),
            ("skala_place_accessorholder_attribute_on_same_line", "always")
        );
        Agrees(
            $$"""
              class C {
                  {{Long3}}
              }
              """,
            """
            class C {
                [Obsolete] /* c */
                public int F = alphaValue + betaValue + gammaValue + deltaValue + epsilonValue + zzzzzzzzzzzzzzzzzzzz;
            }
            """,
            ("skala_place_field_attribute_on_same_line", "always"),
            ("skala_place_accessorholder_attribute_on_same_line", "always")
        );
        Agrees(
            $$"""
              class C {
                  {{Long4}}
              }
              """,
            """
            class C {
                [Obsolete] /* c */
                public int F = Compute(alphaValue, betaValue, gammaValue, deltaValue, eeeeeeeeeeeeeeeeeeeeeeeeee);
            }
            """,
            ("skala_place_field_attribute_on_same_line", "always"),
            ("skala_place_accessorholder_attribute_on_same_line", "always")
        );
        Agrees(
            $$"""
              class C {
                  [Obsolete] /* c */ private static readonly int F = alphaValue + betaValue + {{R('z', 47)}};
              }
              """,
            $$"""
              class C {
                  [Obsolete] /* c */
                  private static readonly int F = alphaValue + betaValue + {{R('z', 47)}};
              }
              """,
            ("skala_place_field_attribute_on_same_line", "always"),
            ("skala_place_accessorholder_attribute_on_same_line", "always")
        );
        Agrees(
            $$"""
              class C {
                  [Obsolete] /* c */ public event System.EventHandler E{{R('z', 65)}};
              }
              """,
            """
            class C {
                [Obsolete] /* c */
                public event System.EventHandler Ezzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
            }
            """,
            ("skala_place_field_attribute_on_same_line", "always"),
            ("skala_place_accessorholder_attribute_on_same_line", "always")
        );
        Agrees(
            $$"""
              class C {
                  [Obsolete] /* c */ public int F = alphaValue ? betaValue : gammaValue + {{R('z', 48)}};
              }
              """,
            """
            class C {
                [Obsolete] /* c */
                public int F = alphaValue ? betaValue : gammaValue + zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
            }
            """,
            ("skala_place_field_attribute_on_same_line", "always"),
            ("skala_place_accessorholder_attribute_on_same_line", "always")
        );
    }
}
