using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #467, SK-DIV-0099: a using alias&apos;s = is the = of every other declaration. Every expected string is
///     <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class UsingAliasIssue467Tests {
    const string Long1 = "using L = System.Collections.Generic.Dictionary<System.Collections.Generic.IRead"
        + "OnlyList<string>, System.Collections.Generic.IReadOnlyDictionary<string, int>>;";

    const string Long2 = "using LongAliasName = System.Collections.Generic.IReadOnlyDictionary<System.Coll"
        + "ections.Generic.IReadOnlyList<string>, System.Collections.Generic.IReadOnlyList<"
        + "int>>;";

    const string Long3 = "using LongAlias = System.Collections.Generic.Dictionary<System.Collections.Gener"
        + "ic.IReadOnlyList<string>, Ixxxxxxxxxxx>;";

    const string Long4 = "using LongAlias = System.Collections.Generic.Dictionary<System.Collections.Gener"
        + "ic.IReadOnlyList<string>, Ixxxxxxxxxxxx>;";

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
    ///     #467: a using alias's = breaks when the line through the type's first break point does not fit, and
    ///     the type below it, kept or added, is one level in at the top of a file too.
    /// </summary>
    [Fact]
    public void AnAlias_BreaksAfterItsEqualsAndIndentsTheType() {
        Agrees(
            """
            using Short =
                (int A, int B);
            """,
            """
            using Short =
                (int A, int B);
            """
        );
        Agrees(
            $$"""
              using Short = (int A, int B);
              {{Long1}}
              """,
            """
            using Short = (int A, int B);
            using L =
                System.Collections.Generic.Dictionary<System.Collections.Generic.IReadOnlyList<string>,
                    System.Collections.Generic.IReadOnlyDictionary<string, int>>;
            """
        );
        Agrees(
            """
            namespace N;

            using Overflows4 =
            (int A, int B);
            """,
            """
            namespace N;

            using Overflows4 =
                (int A, int B);
            """
        );
        Agrees(
            """
            using Arr =
                int[];
            using P =
                int*;
            """,
            """
            using Arr =
                int[];
            using P =
                int*;
            """
        );
        Agrees(
            """
            using X =
                System.String;
            using Y =
                (int A, int B);
            """,
            """
            using X =
                System.String;
            using Y =
                (int A, int B);
            """
        );
        Agrees(
            """
            using System;
            using Y =
                (int A, int B);
            class C { }
            """,
            """
            using System;
            using Y =
                (int A, int B);

            class C { }
            """
        );
        Agrees(
            """
            global using Y =
                (int A, int B);
            """,
            """
            global using Y =
                (int A, int B);
            """
        );
        Agrees(
            $$"""
              {{Long2}}
              """,
            """
            using LongAliasName =
                System.Collections.Generic.IReadOnlyDictionary<System.Collections.Generic.IReadOnlyList<string>,
                    System.Collections.Generic.IReadOnlyList<int>>;
            """
        );
        Agrees(
            $$"""
              {{Long3}}
              """,
            $$"""
              {{Long3}}
              """
        );
        Agrees(
            $$"""
              {{Long4}}
              """,
            """
            using LongAlias =
                System.Collections.Generic.Dictionary<System.Collections.Generic.IReadOnlyList<string>, Ixxxxxxxxxxxx>;
            """
        );
        Agrees(
            $$"""
              using LongAlias = (System.Collections.Generic.IReadOnlyList<string> Names, int C{{R('x', 39)}});
              """,
            """
            using LongAlias =
                (System.Collections.Generic.IReadOnlyList<string> Names, int Cxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx);
            """
        );
        Agrees(
            $$"""
              namespace N {
                  using LongAlias = System.Collections.Generic.Dictionary<string, Namespace.Inner.Type{{R('x', 31)}}>;
              }
              """,
            """
            namespace N {
                using LongAlias =
                    System.Collections.Generic.Dictionary<string, Namespace.Inner.Typexxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>;
            }
            """
        );
    }
}
