using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     SK-DIV-0351: an aligned type parameter list&apos;s head on its angle&apos;s line. Every expected string is <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class AlignedTypeParameterHeadTests {
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

    /// <summary>SK-DIV-0351: under skala_align_multiline_type_parameter_list = true a list whose head on the angle&apos;s line would be narrower than twelve columns breaks after the angle; twelve and wider keep it and align the rest.</summary>
    [Fact]
    public void ANarrowHead_BreaksAfterTheAngle() {
        Agrees(
            """
            public class C {
                public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<TFirstPara, Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy>() { }
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
            """
            public class C {
                public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<TFirstParam, Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy>() { }
            }
            """,
            """
            public class C {
                public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<TFirstParam,
                                                           Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy>() { }
            }
            """,
            ("skala_align_multiline_type_parameter_list", "true")
        );
        Agrees(
            """
            public class C {
                public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<TA, TB, Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy>() { }
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
            """
            public class C {
                public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<TA, TB, TCdef, Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy>() { }
            }
            """,
            """
            public class C {
                public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<TA, TB, TCdef,
                                                           Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy>() { }
            }
            """,
            ("skala_align_multiline_type_parameter_list", "true")
        );
        Agrees(
            """
            public class C {
                public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<TFirst, Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy>() { }
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
            """
            public class C {
                public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<TFirst, TSecond, Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy>() { }
            }
            """,
            """
            public class C {
                public void Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmm<TFirst, TSecond,
                                                           Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy>() { }
            }
            """,
            ("skala_align_multiline_type_parameter_list", "true")
        );
        Agrees(
            """
            public class C {
                public void Mnn<TFirst, Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy>() { }
            }
            """,
            """
            public class C {
                public void Mnn<
                    TFirst, Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy>() { }
            }
            """,
            ("skala_align_multiline_type_parameter_list", "true")
        );
        Agrees(
            """
            public class C {
                public void Mnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn<TFirst, Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy>() { }
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
            """
            public class Widest<TFirstParameterNameXXXXXXXXXX, TSecondParameterNameXXXXXXXXXXX, TThirdParameterNameXXXXXXXXXXXXX, TFourth> { }
            """,
            """
            public class Widest<TFirstParameterNameXXXXXXXXXX, TSecondParameterNameXXXXXXXXXXX, TThirdParameterNameXXXXXXXXXXXXX,
                                TFourth> { }
            """,
            ("skala_align_multiline_type_parameter_list", "true")
        );
        Agrees(
            """
            public class Widest<TFirstParameterNameXXXXXXXXXX, TSecondParameterNameXXXXXXXXXXX, TThirdParameterNameXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX, TFourth> { }
            """,
            """
            public class Widest<TFirstParameterNameXXXXXXXXXX, TSecondParameterNameXXXXXXXXXXX,
                                TThirdParameterNameXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX,
                                TFourth> { }
            """,
            ("skala_align_multiline_type_parameter_list", "true")
        );
        Agrees(
            """
            public class C {
                public void OneParameterWiderThanTheMargin<TFirst, Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy>() { }
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
