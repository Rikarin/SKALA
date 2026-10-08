using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #538: a type parameter list&apos;s angle brackets are a level of their own. Every expected string is <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class TypeParameterAttributeIssue538Tests {
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

    /// <summary>#538: an attribute section whose arguments chop on a type parameter list&apos;s line puts them two levels in and its closer one.</summary>
    [Fact]
    public void ATypeParameterSection_ChopsTwoLevelsIn() {
        Agrees(
            """
            class C<[Description("aaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbb")] [Obsolete] T> { }
            """,
            """
            class C<[Description(
                    "aaaaaaaaaaaaaaaaaaaaaaa",
                    "bbbbbbbbbbbbbbbbb"
                )]
                [Obsolete]
                T> { }
            """
        );
        Agrees(
            """
            class C<[Description("aaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbb")] T> { }
            """,
            """
            class C<[Description(
                    "aaaaaaaaaaaaaaaaaaaaaaa",
                    "bbbbbbbbbbbbbbbbb"
                )]
                T> { }
            """
        );
        Agrees(
            """
            class C<TFirst, [Description("aaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbb")] T> { }
            """,
            """
            class C<TFirst, [Description(
                    "aaaaaaaaaaaaaaaaaaaaaaa",
                    "bbbbbbbbbbbbbbbbb"
                )]
                T> { }
            """
        );
        Agrees(
            """
            class C {
                void M<[Description("aaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbb")] T>() { }
            }
            """,
            """
            class C {
                void M<[Description(
                        "aaaaaaaaaaaaaaaaaaaaaaa",
                        "bbbbbbbbbbbbbbbbb"
                    )]
                    T>() { }
            }
            """
        );
        Agrees(
            """
            class C<[Description("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] T> { }
            """,
            """
            class C<
                [Description(
                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                )]
                T> { }
            """
        );
        Agrees(
            """
            class C<T,
            U> { }
            """,
            """
            class C<T,
                U> { }
            """
        );
    }
}
