using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;
using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #452, SK-DIV-0024: an aligned type parameter list's first break point is the gap after its angle. Every
///     expected string is <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class AlignedTypeParameterIssue452Tests {
    const string Long1 = "public void OneParameterWiderThanTheMargin<TAnAbsolutelyEnormousSingleTypeParame"
        + "terNameThatOverflowsTheMarginOnItsOwn>() { }";

    const string Long2 = "public class Widest<TFirstParameterNameXXXXXXXXXX, TSecondParameterNameXXXXXXXXX"
        + "XX, TThirdParameterNameXXXXXXXXXXXXX, TFourth> { }";

    const string Long3 = "public class Widest<TFirstParameterNameXXXXXXXXXX, TSecondParameterNameXXXXXXXXX"
        + "XX, TThirdParameterNameXXXXXXXXXXXXX,";

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
    ///     #452: under skala_align_multiline_type_parameter_list = true a single type parameter that does not fit moves
    ///     below the angle, as at false, from 121 columns; a list that fills keeps its parameters under the first.
    /// </summary>
    [Fact]
    public void ASingleTypeParameterWiderThanTheMargin_BreaksAfterTheAngle() =>
        Agrees(
            $$"""
              public class SingleTypeParameter {
                  {{Long1}}
              }

              public class C {
                  public void OneParameterWiderThanTheMargin<T{{R('x', 65)}}>() { }
              }

              public class C {
                  public void OneParameterWiderThanTheMargin<T{{R('x', 66)}}>() { }
              }

              public class C {
                  public void OneParameterWiderThanTheMargin<T{{R('x', 67)}}>() { }
              }

              public class C {
                  public void OneParameterWiderThanTheMargin<T{{R('x', 69)}}>() { }
              }

              {{Long2}}

              public class C {
                  public void ManyParams<TFirstParameterName,
                      TSecondParameterName>(int a) { }
              }
              """,
            $$"""
              public class SingleTypeParameter {
                  public void OneParameterWiderThanTheMargin<
                      TAnAbsolutelyEnormousSingleTypeParameterNameThatOverflowsTheMarginOnItsOwn>() { }
              }

              public class C {
                  public void OneParameterWiderThanTheMargin<T{{R('x', 65)}}>() { }
              }

              public class C {
                  public void OneParameterWiderThanTheMargin<
                      Txxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>() { }
              }

              public class C {
                  public void OneParameterWiderThanTheMargin<
                      Txxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>() { }
              }

              public class C {
                  public void OneParameterWiderThanTheMargin<
                      Txxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>() { }
              }

              {{Long3}}
                                  TFourth> { }

              public class C {
                  public void ManyParams<TFirstParameterName,
                                         TSecondParameterName>(int a) { }
              }
              """,
            ("skala_align_multiline_type_parameter_list", "true")
        );
}
