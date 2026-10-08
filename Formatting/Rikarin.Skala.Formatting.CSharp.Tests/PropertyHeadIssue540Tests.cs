using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #540, SK-DIV-0127: a property&apos;s modifiers/type and type/name gaps. Every expected string is
///     <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class PropertyHeadIssue540Tests {
    const string Long1 = "public static System.Collections.Generic.IReadOnlyDictionary<string, System.Coll"
        + "ections.Generic.IReadOnlyList<Sxx>> Property { get; set; }";

    const string Long2 = "public static System.Collections.Generic.IReadOnlyDictionary<string, System.Coll"
        + "ections.Generic.IReadOnlyList<Sxx>>";

    const string Long3 = "public static System.Collections.Generic.IReadOnlyDictionary<string, System.Coll"
        + "ections.Generic.IReadOnlyList<Sxxxx>> Property { get; set; }";

    const string Long4 = "System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generi"
        + "c.IReadOnlyList<Sxxxx>>";

    const string Long5 = "public static System.Collections.Generic.IReadOnlyDictionary<string, System.Coll"
        + "ections.Generic.IReadOnlyList<Sxxxxxxxx>> Property { get; set; }";

    const string Long6 = "System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generi"
        + "c.IReadOnlyList<Sxxxxxxxx>>";

    const string Long7 = "public static System.Collections.Generic.IReadOnlyDictionary<string, System.Coll"
        + "ections.Generic.IReadOnlyList<S>> Property => null;";

    const string Long8 = "public static System.Collections.Generic.IReadOnlyDictionary<string, System.Coll"
        + "ections.Generic.IReadOnlyList<S>>";

    const string Long9 = "public static System.Collections.Generic.IReadOnlyDictionary<string, System.Coll"
        + "ections.Generic.IReadOnlyList<Sxxxx>> Property => null;";

    const string Long10 = "public static System.Collections.Generic.IReadOnlyDictionary<string, System.Coll"
        + "ections.Generic.IReadOnlyList<Sxxxxxx>> Property { get; }";

    const string Long11 = "System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generi"
        + "c.IReadOnlyList<Sxxxxxx>>";

    const string Long12 = "public static System.Collections.Generic.IReadOnlyDictionary<string, System.Coll"
        + "ections.Generic.IReadOnlyList<Sxx>> P { get; set; }";

    const string Long13 = "public static System.Collections.Generic.IReadOnlyDictionary<string, System.Coll"
        + "ections.Generic.IReadOnlyList<Sxxxx>> P { get; set; }";

    const string Long14 = "System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generi"
        + "c.IReadOnlyList<Sxxxx>> P {";

    const string Long15 = "const System.Collections.Generic.IReadOnlyDictionary<string, System.Collections."
        + "Generic.IReadOnlyList<Sxxxx>> local = null;";

    const string Long16 = "const System.Collections.Generic.IReadOnlyDictionary<string, System.Collections."
        + "Generic.IReadOnlyList<Sxxxx>>";

    const string Long17 = "const System.Collections.Generic.IReadOnlyDictionary<string, System.Collections."
        + "Generic.IReadOnlyList<Sxxxxxxxx>> local = null;";

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
    ///     #540: a property breaks between its modifiers and its type when the type ends past 120, and before its name
    ///     when the line through the accessor list's brace or the arrow does not fit; a const local keeps its type on
    ///     the const line.
    /// </summary>
    [Fact]
    public void APropertysHead_BreaksAsAFieldsDoes() {
        Agrees(
            $$"""
              class C {
                  {{Long1}}
              }
              """,
            $$"""
              class C {
                  {{Long2}}
                      Property { get; set; }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{Long3}}
              }
              """,
            $$"""
              class C {
                  public static
                      {{Long4}}
                      Property { get; set; }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{Long5}}
              }
              """,
            $$"""
              class C {
                  public static
                      {{Long6}}
                      Property { get; set; }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{Long7}}
              }
              """,
            $$"""
              class C {
                  {{Long8}}
                      Property =>
                      null;
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{Long9}}
              }
              """,
            $$"""
              class C {
                  public static
                      {{Long4}}
                      Property =>
                      null;
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{Long10}}
              }
              """,
            $$"""
              class C {
                  public static
                      {{Long11}}
                      Property { get; }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{Long12}}
              }
              """,
            $$"""
              class C {
                  {{Long2}}
                      P { get; set; }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{Long13}}
              }
              """,
            $$"""
              class C {
                  public static
                      {{Long14}}
                      get;
                      set;
                  }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  void M() {
                      {{Long15}}
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      {{Long16}}
                          local = null;
                  }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  void M() {
                      {{Long17}}
                  }
              }
              """,
            """
            class C {
                void M() {
                    const System.Collections.Generic.IReadOnlyDictionary<string,
                        System.Collections.Generic.IReadOnlyList<Sxxxxxxxx>> local = null;
                }
            }
            """
        );
    }
}
