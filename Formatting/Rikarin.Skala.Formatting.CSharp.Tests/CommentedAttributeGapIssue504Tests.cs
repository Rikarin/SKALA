using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #504, SK-DIV-0201: a field&apos;s gap after its last attribute section and a block comment. Every expected string is <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class CommentedAttributeGapIssue504Tests {
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

    /// <summary>#504: a field with a block comment after its last attribute section is declined when the joined line overflows, and a call or a creation only while its terminator alone overflows.</summary>
    [Fact]
    public void ACommentAfterTheAttribute_DeclinesTheJoinPastTheMargin() {
        Agrees(
            """
            class C {
                [Obsolete] /* c */ public int F = alphaValue + betaValue + gammaValue + deltaValue + epsilonValue + zzzzzzzzzzzzzzz;
            }
            """,
            """
            class C {
                [Obsolete] /* c */ public int F = alphaValue + betaValue + gammaValue + deltaValue + epsilonValue + zzzzzzzzzzzzzzz;
            }
            """
        );
        Agrees(
            """
            class C {
                [Obsolete] /* c */ public int F = alphaValue + betaValue + gammaValue + deltaValue + epsilonValue + zzzzzzzzzzzzzzzz;
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
            """
            class C {
                [Obsolete] /* c */ public int F = alphaValue + betaValue + gammaValue + deltaValue + epsilonValue + zzzzzzzzzzzzzzzzzzzz;
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
            """
            class C {
                [Obsolete] /* c */ public int F = vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv;
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
            """
            class C {
                [Obsolete] /* c */ public string F = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
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
            """
            class C {
                [Obsolete] /* c */ public int F = Compute(alphaValue, betaValue, gammaValue, deltaValue, eeeeeeeeeeeeeeeeeeeeeeeeee);
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
            """
            class C {
                [Obsolete] /* c */ public int F = alphaValue ? betaValue : gammaValue + zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
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
            """
            class C {
                [Obsolete] /* c */ public int F = alphaValue ? betaValue : gammaValue + zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
            }
            """,
            """
            class C {
                [Obsolete] /* c */
                public int F = alphaValue ? betaValue : gammaValue + zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
            }
            """
        );
        Agrees(
            """
            class C {
                [Obsolete] /* c */ private static readonly int F = alphaValue + betaValue + zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
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
            """
            class C {
                [Obsolete] /* c */ private static readonly int F = alphaValue + betaValue + zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
            }
            """,
            """
            class C {
                [Obsolete] /* c */
                private static readonly int F = alphaValue + betaValue + zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
            }
            """
        );
        Agrees(
            """
            class C {
                [Obsolete] /* c */ public event System.EventHandler Ezzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
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
            """
            class C {
                [Obsolete] /* c */ public int F = alpha.Beta.Gamma.Delta.Epsilon.Compute().Zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
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
            """
            class C {
                [Obsolete] /* c */ public int P { get; set; } = alphaValue + betaValue + zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
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
            """
            class C {
                [Obsolete] /* c */ public int F = alphaValue + betaValue + gammaValue + deltaValue + epsilonValue + zzzzzzzzzzzzzzz;
            }
            """,
            """
            class C {
                [Obsolete] /* c */ public int F = alphaValue + betaValue + gammaValue + deltaValue + epsilonValue + zzzzzzzzzzzzzzz;
            }
            """,
            ("skala_place_field_attribute_on_same_line", "always"),
            ("skala_place_accessorholder_attribute_on_same_line", "always")
        );
        Agrees(
            """
            class C {
                [Obsolete] /* c */ public int F = alphaValue + betaValue + gammaValue + deltaValue + epsilonValue + zzzzzzzzzzzzzzzz;
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
            """
            class C {
                [Obsolete] /* c */ public int F = alphaValue + betaValue + gammaValue + deltaValue + epsilonValue + zzzzzzzzzzzzzzzzzzzz;
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
            """
            class C {
                [Obsolete] /* c */ public int F = Compute(alphaValue, betaValue, gammaValue, deltaValue, eeeeeeeeeeeeeeeeeeeeeeeeee);
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
            """
            class C {
                [Obsolete] /* c */ private static readonly int F = alphaValue + betaValue + zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
            }
            """,
            """
            class C {
                [Obsolete] /* c */
                private static readonly int F = alphaValue + betaValue + zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
            }
            """,
            ("skala_place_field_attribute_on_same_line", "always"),
            ("skala_place_accessorholder_attribute_on_same_line", "always")
        );
        Agrees(
            """
            class C {
                [Obsolete] /* c */ public event System.EventHandler Ezzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
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
            """
            class C {
                [Obsolete] /* c */ public int F = alphaValue ? betaValue : gammaValue + zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;
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
