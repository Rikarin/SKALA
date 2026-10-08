using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #539, SK-DIV-0353: a type declaration&apos;s keyword/name gap. Every expected string is <c>jb cleanupcode</c>
///     2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class TypeNameGapIssue539Tests {
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
    ///     #539: a type header breaks between its keyword and its name when the line runs past the margin before its next
    ///     point, or when the rest fits below within four columns (a lone base type: none); the base list then sits at the
    ///     declaration&apos;s own level.
    /// </summary>
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

    /// <summary>
    ///     #539 round three: a record, a record struct or a class with a parameter list keeps its name on the keyword's line at every width measured; the parameters or the base list take the break.
    /// </summary>
    [Fact]
    public void ATypeWithAPrimaryConstructor_NeverBreaksBeforeItsName() {
        Agrees(
            """
            public sealed record Nnnnnnnnnn(int Alpha = 0, string Beta = "b", long Gammaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = 1) {
            }
            """,
            """
            public sealed record Nnnnnnnnnn(
                int Alpha = 0,
                string Beta = "b",
                long Gammaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = 1) { }
            """
        );
        Agrees(
            """
            public sealed partial record struct Nnnnnnnnnn<T>(Dictionary<string, int> p1, long p2, byte p3, object paaaaaaaaaaaaaaaaaaaaa) {
            }
            """,
            """
            public sealed partial record struct Nnnnnnnnnn<T>(
                Dictionary<string, int> p1,
                long p2,
                byte p3,
                object paaaaaaaaaaaaaaaaaaaaa) { }
            """
        );
        Agrees(
            """
            public class Nnnnnnnnnnnnnnnnnnnnnnnnnnnnnn(int alpha, string beta) : BaseType(alpha), IFoooooooooooooooooooooooooooooo {
            }
            """,
            """
            public class Nnnnnnnnnnnnnnnnnnnnnnnnnnnnnn(int alpha, string beta)
                : BaseType(alpha), IFoooooooooooooooooooooooooooooo { }
            """
        );
    }

    /// <summary>
    ///     #539 round three: the window is the joined line's overflow, not what the continuation line saves: behind <c>internal sealed class</c> the oracle breaks the name at 124 and not at 125.
    /// </summary>
    [Fact]
    public void TheNameBreaksOnlyUpTo124Columns_WhateverTheHead() {
        Agrees(
            """
            internal sealed class Nnnnnnnnnn : IAlphaInterfaceNameValue, IGammaggggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """,
            """
            internal sealed class
                Nnnnnnnnnn : IAlphaInterfaceNameValue, IGammaggggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """
        );
        Agrees(
            """
            internal sealed class Nnnnnnnnnn : IAlphaInterfaceNameValue, IGammagggggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """,
            """
            internal sealed class Nnnnnnnnnn : IAlphaInterfaceNameValue,
                IGammagggggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """
        );
    }

    /// <summary>
    ///     #539 round three: <c>9·column + 6·name − 3·first item + 807 ≥ 8·end</c>: an 18-letter name behind <c>public class</c> stays, a 20-letter one breaks at 122 and not at 123, an 8-letter one behind <c>public sealed class</c> breaks at 121, and three interfaces answer as two.
    /// </summary>
    [Fact]
    public void AShortNameBehindAShortHead_StaysAndTheListWraps() {
        Agrees(
            """
            public class Nnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue, IGammaggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """,
            """
            public class Nnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue,
                IGammaggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """
        );
        Agrees(
            """
            public class Nnnnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue, IGammagggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """,
            """
            public class
                Nnnnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue, IGammagggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """
        );
        Agrees(
            """
            public class Nnnnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue, IGammaggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """,
            """
            public class Nnnnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue,
                IGammaggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """
        );
        Agrees(
            """
            public sealed class Nnnnnnnn : IAlphaInterfaceNameValue, IGammagggggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """,
            """
            public sealed class
                Nnnnnnnn : IAlphaInterfaceNameValue, IGammagggggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """
        );
        Agrees(
            """
            public sealed class Nnnnnnnn : IAlphaInterfaceNameValue, IGammaggggggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """,
            """
            public sealed class Nnnnnnnn : IAlphaInterfaceNameValue,
                IGammaggggggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """
        );
        Agrees(
            """
            public class Nnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue, IBetaInterfaceNameValue, IGammagggggggggggggggggggggggggg { }
            """,
            """
            public class Nnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue,
                IBetaInterfaceNameValue,
                IGammagggggggggggggggggggggggggg { }
            """
        );
    }

    /// <summary>
    ///     #539 round three: a 10-letter name breaks at 121 before a 4-letter first base type, a 14-letter one does not before a 20-letter one, and a first base type past 22 letters stops counting.
    /// </summary>
    [Fact]
    public void TheFirstBaseTypesWidth_MovesTheThreshold() {
        Agrees(
            """
            public class Nnnnnnnnnn : Ifff, IGammaggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """,
            """
            public class
                Nnnnnnnnnn : Ifff, IGammaggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """
        );
        Agrees(
            """
            public class Nnnnnnnnnnnnnn : Ifffffffffffffffffff, IGammaggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """,
            """
            public class Nnnnnnnnnnnnnn : Ifffffffffffffffffff,
                IGammaggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """
        );
        Agrees(
            """
            public class Nnnnnnnnnnnnnnnnnnnnnn : Ifffffffffffffffffffffffffffffffffff, IGammaggggggggggggggggggggggggggggggggggggg { }
            """,
            """
            public class
                Nnnnnnnnnnnnnnnnnnnnnn : Ifffffffffffffffffffffffffffffffffff, IGammaggggggggggggggggggggggggggggggggggggg { }
            """
        );
        Agrees(
            """
            public class Nnnnnnnnnnnnnnnnnn : Ifffffffffffffffffffffffffffffffffff, IGammaggggggggggggggggggggggggggggggggggggggg { }
            """,
            """
            public class Nnnnnnnnnnnnnnnnnn : Ifffffffffffffffffffffffffffffffffff,
                IGammaggggggggggggggggggggggggggggggggggggggg { }
            """
        );
    }

    /// <summary>
    ///     #539 round three: with two type parameters and no base list the list's comma competes the same way, from its own constant: a 25-letter name stays at 121, a 30-letter one breaks to 123.
    /// </summary>
    [Fact]
    public void TwoTypeParameters_TheListsCommaTakesAShortName() {
        Agrees(
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNN<TFirstParameterName, TSecondxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """,
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNN<TFirstParameterName,
                TSecondxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """
        );
        Agrees(
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirstParameterName, TSecondxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """,
            """
            public class
                NNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirstParameterName, TSecondxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """
        );
        Agrees(
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirstParameterName, TSecondxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """,
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirstParameterName,
                TSecondxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """
        );
    }

    /// <summary>
    ///     #539 round three: behind <c>class</c> alone the name never moves down for the base list's sake, but it does when the name itself runs past the margin, and before a lone base type.
    /// </summary>
    [Fact]
    public void ABareKeyword_KeepsTheNameForAList_ButNotForALoneBaseType() {
        Agrees(
            """
            class Nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue, IGammaggggggggggggggggggggggggggggggggggggg { }
            """,
            """
            class Nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue,
                IGammaggggggggggggggggggggggggggggggggggggg { }
            """
        );
        Agrees(
            """
            class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN : IFoo { }
            """,
            """
            class
                NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN : IFoo { }
            """
        );
        Agrees(
            """
            class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN { }
            """,
            """
            class
                NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN { }
            """
        );
    }

    /// <summary>
    ///     #539 round three: the oracle measures the header as if <c>{ }</c> ended it: with a member inside, the name breaks while the line through the <c>{</c> is 122 columns and not at 123.
    /// </summary>
    [Fact]
    public void ABodyBelowTheBrace_CountsAsIfItsBraceFollowed() {
        Agrees(
            """
            public class Nnnnnnnnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue, IGammagggggggggggggggggggggggggggggggggggggggggggggggg {
                int x;
            }
            """,
            """
            public class
                Nnnnnnnnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue, IGammagggggggggggggggggggggggggggggggggggggggggggggggg {
                int x;
            }
            """
        );
        Agrees(
            """
            public class Nnnnnnnnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue, IGammaggggggggggggggggggggggggggggggggggggggggggggggggg {
                int x;
            }
            """,
            """
            public class Nnnnnnnnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue,
                IGammaggggggggggggggggggggggggggggggggggggggggggggggggg {
                int x;
            }
            """
        );
        Agrees(
            """
            public abstract partial class Nnnnnnnnnn : IAlphaInterfaceNameValue, IGammagggggggggggggggggggggggggggggggggggggggggggggg {
                int x;
            }
            """,
            """
            public abstract partial class Nnnnnnnnnn : IAlphaInterfaceNameValue,
                IGammagggggggggggggggggggggggggggggggggggggggggggggg {
                int x;
            }
            """
        );
    }
}
