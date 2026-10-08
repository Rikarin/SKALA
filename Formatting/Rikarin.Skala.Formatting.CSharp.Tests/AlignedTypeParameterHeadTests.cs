using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     SK-DIV-0351: an aligned type parameter list&apos;s head on its angle&apos;s line. Every expected string is
///     <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class AlignedTypeParameterHeadTests {
    const string Long1 = "public class Widest<TFirstParameterNameXXXXXXXXXX, TSecondParameterNameXXXXXXXXX"
        + "XX, TThirdParameterNameXXXXXXXXXXXXX, TFourth> { }";

    const string Long2 = "public class Widest<TFirstParameterNameXXXXXXXXXX, TSecondParameterNameXXXXXXXXX"
        + "XX, TThirdParameterNameXXXXXXXXXXXXX,";

    const string Long3 = "public class Widest<TFirstParameterNameXXXXXXXXXX, TSecondParameterNameXXXXXXXXX"
        + "XX, TThirdParameterNameXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"
        + "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX, TFourth> { }";

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
    ///     SK-DIV-0351: under skala_align_multiline_type_parameter_list = true a list whose head on the angle's line
    ///     would be narrower than twelve columns breaks after the angle; twelve and wider keep it and align the rest.
    /// </summary>
    [Fact]
    public void ANarrowHead_BreaksAfterTheAngle() {
        Agrees(
            $$"""
              public class C {
                  public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<TFirstPara, T{{R('y', 55)}}>() { }
              }
              """,
            """
            public class C {
                public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<
                    TFirstPara, Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy>() { }
            }
            """,
            ("skala_align_multiline_type_parameter_list", "true")
        );
        Agrees(
            $$"""
              public class C {
                  public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<TFirstParam, T{{R('y', 54)}}>() { }
              }
              """,
            $$"""
              public class C {
                  public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<TFirstParam,
                                                             T{{R('y', 54)}}>() { }
              }
              """,
            ("skala_align_multiline_type_parameter_list", "true")
        );
        Agrees(
            $$"""
              public class C {
                  public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<TA, TB, T{{R('y', 62)}}>() { }
              }
              """,
            """
            public class C {
                public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<
                    TA, TB, Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy>() { }
            }
            """,
            ("skala_align_multiline_type_parameter_list", "true")
        );
        Agrees(
            $$"""
              public class C {
                  public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<TA, TB, TCdef, T{{R('y', 55)}}>() { }
              }
              """,
            $$"""
              public class C {
                  public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<TA, TB, TCdef,
                                                             T{{R('y', 55)}}>() { }
              }
              """,
            ("skala_align_multiline_type_parameter_list", "true")
        );
        Agrees(
            $$"""
              public class C {
                  public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<TFirst, T{{R('y', 65)}}>() { }
              }
              """,
            """
            public class C {
                public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<
                    TFirst, Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy>() { }
            }
            """,
            ("skala_align_multiline_type_parameter_list", "true")
        );
        Agrees(
            $$"""
              public class C {
                  public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<TFirst, TSecond, T{{R('y', 56)}}>() { }
              }
              """,
            $$"""
              public class C {
                  public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<TFirst, TSecond,
                                                             T{{R('y', 56)}}>() { }
              }
              """,
            ("skala_align_multiline_type_parameter_list", "true")
        );
        Agrees(
            $$"""
              public class C {
                  public void Mnn<TFirst, T{{R('y', 86)}}>() { }
              }
              """,
            $$"""
              public class C {
                  public void Mnn<
                      TFirst, T{{R('y', 86)}}>() { }
              }
              """,
            ("skala_align_multiline_type_parameter_list", "true")
        );
        Agrees(
            $$"""
              public class C {
                  public void Mnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn<TFirst, T{{R('y', 69)}}>() { }
              }
              """,
            """
            public class C {
                public void Mnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn<
                    TFirst, Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy>() { }
            }
            """,
            ("skala_align_multiline_type_parameter_list", "true")
        );
        Agrees(
            $$"""
              {{Long1}}
              """,
            $$"""
              {{Long2}}
                                  TFourth> { }
              """,
            ("skala_align_multiline_type_parameter_list", "true")
        );
        Agrees(
            $$"""
              {{Long3}}
              """,
            $$"""
              public class Widest<TFirstParameterNameXXXXXXXXXX, TSecondParameterNameXXXXXXXXXXX,
                                  TThirdParameterName{{R('X', 87)}},
                                  TFourth> { }
              """,
            ("skala_align_multiline_type_parameter_list", "true")
        );
        Agrees(
            $$"""
              public class C {
                  public void OneParameterWiderThanTheMargin<TFirst, T{{R('y', 58)}}>() { }
              }
              """,
            """
            public class C {
                public void OneParameterWiderThanTheMargin<
                    TFirst, Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy>() { }
            }
            """,
            ("skala_align_multiline_type_parameter_list", "true")
        );
    }
}
