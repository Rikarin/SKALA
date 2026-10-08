using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #540, SK-DIV-0127: a property&apos;s modifiers/type and type/name gaps. Every expected string is <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class PropertyHeadIssue540Tests {
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

    /// <summary>#540: a property breaks between its modifiers and its type when the type ends past 120, and before its name when the line through the accessor list&apos;s brace or the arrow does not fit; a const local keeps its type on the const line.</summary>
    [Fact]
    public void APropertysHead_BreaksAsAFieldsDoes() {
        Agrees(
            """
            class C {
                public static System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<Sxx>> Property { get; set; }
            }
            """,
            """
            class C {
                public static System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<Sxx>>
                    Property { get; set; }
            }
            """
        );
        Agrees(
            """
            class C {
                public static System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<Sxxxx>> Property { get; set; }
            }
            """,
            """
            class C {
                public static
                    System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<Sxxxx>>
                    Property { get; set; }
            }
            """
        );
        Agrees(
            """
            class C {
                public static System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<Sxxxxxxxx>> Property { get; set; }
            }
            """,
            """
            class C {
                public static
                    System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<Sxxxxxxxx>>
                    Property { get; set; }
            }
            """
        );
        Agrees(
            """
            class C {
                public static System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<S>> Property => null;
            }
            """,
            """
            class C {
                public static System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<S>>
                    Property =>
                    null;
            }
            """
        );
        Agrees(
            """
            class C {
                public static System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<Sxxxx>> Property => null;
            }
            """,
            """
            class C {
                public static
                    System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<Sxxxx>>
                    Property =>
                    null;
            }
            """
        );
        Agrees(
            """
            class C {
                public static System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<Sxxxxxx>> Property { get; }
            }
            """,
            """
            class C {
                public static
                    System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<Sxxxxxx>>
                    Property { get; }
            }
            """
        );
        Agrees(
            """
            class C {
                public static System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<Sxx>> P { get; set; }
            }
            """,
            """
            class C {
                public static System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<Sxx>>
                    P { get; set; }
            }
            """
        );
        Agrees(
            """
            class C {
                public static System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<Sxxxx>> P { get; set; }
            }
            """,
            """
            class C {
                public static
                    System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<Sxxxx>> P {
                    get;
                    set;
                }
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    const System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<Sxxxx>> local = null;
                }
            }
            """,
            """
            class C {
                void M() {
                    const System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<Sxxxx>>
                        local = null;
                }
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    const System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<Sxxxxxxxx>> local = null;
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
