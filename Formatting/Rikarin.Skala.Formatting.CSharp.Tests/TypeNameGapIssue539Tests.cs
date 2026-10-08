using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #539, SK-DIV-0353: a type declaration&apos;s keyword/name gap. Every expected string is <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class TypeNameGapIssue539Tests {
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

    /// <summary>#539: a type header breaks between its keyword and its name when the line runs past the margin before its next point, or when the rest fits below within four columns (a lone base type: none); the base list then sits at the declaration&apos;s own level.</summary>
    [Fact]
    public void AHeaderPastTheMargin_BreaksBetweenTheKeywordAndTheName() {
        Agrees(
            """
            public class NNNNNNNNNN<Txxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """,
            """
            public class
                NNNNNNNNNN<Txxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """
        );
        Agrees(
            """
            public class NNNNNNNNNN<Txxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """,
            """
            public class
                NNNNNNNNNN<Txxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """
        );
        Agrees(
            """
            public class NNNNNNNNNN<Txxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """,
            """
            public class NNNNNNNNNN<
                Txxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """
        );
        Agrees(
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirst, TSecond> : IFoo, Iyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy { }
            """,
            """
            public class
                NNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirst, TSecond> : IFoo, Iyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy { }
            """
        );
        Agrees(
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirst, TSecond> : IFoo, Iyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy { }
            """,
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirst, TSecond> : IFoo,
                Iyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy { }
            """
        );
        Agrees(
            """
            public class NNNNNNNNNNNNNNN : IFoo, Iyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy { }
            """,
            """
            public class
                NNNNNNNNNNNNNNN : IFoo, Iyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy { }
            """
        );
        Agrees(
            """
            public class NNNNNNNNNNNNNNN : IFoo, Iyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy { }
            """,
            """
            public class NNNNNNNNNNNNNNN : IFoo,
                Iyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy { }
            """
        );
        Agrees(
            """
            public class NNNNNNNNNNNNNNNNNNNN : Iyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy { }
            """,
            """
            public class
                NNNNNNNNNNNNNNNNNNNN : Iyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy { }
            """
        );
        Agrees(
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirstParameterName, TSecondxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """,
            """
            public class
                NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirstParameterName, TSecondxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """
        );
        Agrees(
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirstParameterName, TSecondxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """,
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirstParameterName,
                TSecondxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """
        );
        Agrees(
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNyyyy : IFoo, IBar {
                int x;
                void M() { }
            }
            """,
            """
            public class
                NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNyyyy : IFoo,
                IBar {
                int x;
                void M() { }
            }
            """
        );
        Agrees(
            """
            namespace X {
                public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN : IFoo, IBar {
                    int x;
                }
            }
            """,
            """
            namespace X {
                public class
                    NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN : IFoo,
                    IBar {
                    int x;
                }
            }
            """
        );
        Agrees(
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNxx<T> where T : class {
                int x;
            }
            """,
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNxx<T>
                where T : class {
                int x;
            }
            """
        );
        Agrees(
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNxxxxxxxxx {
                int x;
            }
            """,
            """
            public class
                NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNxxxxxxxxx {
                int x;
            }
            """
        );
        Agrees(
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirst, TSecond> : IFoo, Ibbbbbbbbbbbbbbbbbbbbbbbbbbbbbb {
                int x;
            }
            """,
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirst, TSecond> : IFoo,
                Ibbbbbbbbbbbbbbbbbbbbbbbbbbbbbb {
                int x;
            }
            """
        );
        Agrees(
            """
            [Serializable]
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirstParameterName, TSecondParameterNameXX> {
                int x;
            }
            """,
            """
            [Serializable]
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirstParameterName, TSecondParameterNameXX> {
                int x;
            }
            """
        );
        Agrees(
            """
            public record NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNxxxxxxx(int A, int B);
            """,
            """
            public record NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNxxxxxxx(
                int A,
                int B);
            """
        );
        Agrees(
            """
            public interface INNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNxxxxxxx {
            }
            """,
            """
            public interface
                INNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNxxxxxxx { }
            """
        );
        Agrees(
            """
            public struct NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNxxxxxxxxxxxx {
            }
            """,
            """
            public struct
                NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNxxxxxxxxxxxx { }
            """
        );
    }
}
