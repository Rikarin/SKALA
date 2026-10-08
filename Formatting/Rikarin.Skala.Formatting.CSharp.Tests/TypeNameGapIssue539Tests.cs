using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #539, SK-DIV-0353: a type declaration&apos;s keyword/name gap. Every expected string is <c>jb cleanupcode</c>
///     2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class TypeNameGapIssue539Tests {
    const string Long1 = "public sealed partial record struct Nnnnnnnnnn<T>(Dictionary<string, int> p1, lo"
        + "ng p2, byte p3, object paaaaaaaaaaaaaaaaaaaaa) {";

    const string Long2 = "public abstract class LogEventPropertyValueRewriter<TState> : LogEventPropertyVa"
        + "lueVisitor<TState, Lzzzzzzzzzzzzzzzzzzzzz> {";

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
            $$"""
              public class NNNNNNNNNN<T{{R('x', 91)}}> { }
              """,
            $$"""
              public class
                  NNNNNNNNNN<T{{R('x', 91)}}> { }
              """
        );
        Agrees(
            $$"""
              public class NNNNNNNNNN<T{{R('x', 94)}}> { }
              """,
            $$"""
              public class
                  NNNNNNNNNN<T{{R('x', 94)}}> { }
              """
        );
        Agrees(
            $$"""
              public class NNNNNNNNNN<T{{R('x', 95)}}> { }
              """,
            """
            public class NNNNNNNNNN<
                Txxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """
        );
        Agrees(
            $$"""
              public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirst, TSecond> : IFoo, I{{R('y', 50)}} { }
              """,
            $$"""
              public class
                  NNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirst, TSecond> : IFoo, I{{R('y', 50)}} { }
              """
        );
        Agrees(
            $$"""
              public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirst, TSecond> : IFoo, I{{R('y', 51)}} { }
              """,
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirst, TSecond> : IFoo,
                Iyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy { }
            """
        );
        Agrees(
            $$"""
              public class NNNNNNNNNNNNNNN : IFoo, I{{R('y', 82)}} { }
              """,
            $$"""
              public class
                  NNNNNNNNNNNNNNN : IFoo, I{{R('y', 82)}} { }
              """
        );
        Agrees(
            $$"""
              public class NNNNNNNNNNNNNNN : IFoo, I{{R('y', 83)}} { }
              """,
            """
            public class NNNNNNNNNNNNNNN : IFoo,
                Iyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy { }
            """
        );
        Agrees(
            $$"""
              public class NNNNNNNNNNNNNNNNNNNN : I{{R('y', 87)}} { }
              """,
            $$"""
              public class
                  NNNNNNNNNNNNNNNNNNNN : I{{R('y', 87)}} { }
              """
        );
        Agrees(
            $$"""
              public class {{R('N', 40)}}<TFirstParameterName, TSecondxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
              """,
            $$"""
              public class
                  {{R('N', 40)}}<TFirstParameterName, TSecondxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
              """
        );
        Agrees(
            $$"""
              public class {{R('N', 40)}}<TFirstParameterName, TSecondxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
              """,
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirstParameterName,
                TSecondxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """
        );
        Agrees(
            $$"""
              public class {{R('N', 100)}}yyyy : IFoo, IBar {
                  int x;
                  void M() { }
              }
              """,
            $$"""
              public class
                  {{R('N', 100)}}yyyy : IFoo,
                  IBar {
                  int x;
                  void M() { }
              }
              """
        );
        Agrees(
            $$"""
              namespace X {
                  public class {{R('N', 100)}} : IFoo, IBar {
                      int x;
                  }
              }
              """,
            $$"""
              namespace X {
                  public class
                      {{R('N', 100)}} : IFoo,
                      IBar {
                      int x;
                  }
              }
              """
        );
        Agrees(
            $$"""
              public class {{R('N', 100)}}xx<T> where T : class {
                  int x;
              }
              """,
            $$"""
              public class {{R('N', 100)}}xx<T>
                  where T : class {
                  int x;
              }
              """
        );
        Agrees(
            $$"""
              public class {{R('N', 100)}}xxxxxxxxx {
                  int x;
              }
              """,
            $$"""
              public class
                  {{R('N', 100)}}xxxxxxxxx {
                  int x;
              }
              """
        );
        Agrees(
            $$"""
              public class {{R('N', 60)}}<TFirst, TSecond> : IFoo, Ibbbbbbbbbbbbbbbbbbbbbbbbbbbbbb {
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
            $$"""
              [Serializable]
              public class {{R('N', 60)}}<TFirstParameterName, TSecondParameterNameXX> {
                  int x;
              }
              """,
            $$"""
              [Serializable]
              public class {{R('N', 60)}}<TFirstParameterName, TSecondParameterNameXX> {
                  int x;
              }
              """
        );
        Agrees(
            $$"""
              public record {{R('N', 90)}}xxxxxxx(int A, int B);
              """,
            $$"""
              public record {{R('N', 90)}}xxxxxxx(
                  int A,
                  int B);
              """
        );
        Agrees(
            $$"""
              public interface I{{R('N', 100)}}xxxxxxx {
              }
              """,
            $$"""
              public interface
                  I{{R('N', 100)}}xxxxxxx { }
              """
        );
        Agrees(
            $$"""
              public struct {{R('N', 100)}}xxxxxxxxxxxx {
              }
              """,
            $$"""
              public struct
                  {{R('N', 100)}}xxxxxxxxxxxx { }
              """
        );
    }

    /// <summary>
    ///     #539 round three: a record, a record struct or a class with a parameter list keeps its name on the keyword's line
    ///     at every width measured; the parameters or the base list take the break.
    /// </summary>
    [Fact]
    public void ATypeWithAPrimaryConstructor_NeverBreaksBeforeItsName() {
        Agrees(
            $$"""
              public sealed record Nnnnnnnnnn(int Alpha = 0, string Beta = "b", long Gamm{{R('a', 45)}} = 1) {
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
            $$"""
              {{Long1}}
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
            $$"""
              public class Nnnnnnnnnnnnnnnnnnnnnnnnnnnnnn(int alpha, string beta) : BaseType(alpha), IF{{R('o', 30)}} {
              }
              """,
            """
            public class Nnnnnnnnnnnnnnnnnnnnnnnnnnnnnn(int alpha, string beta)
                : BaseType(alpha), IFoooooooooooooooooooooooooooooo { }
            """
        );
    }

    /// <summary>
    ///     #539 round three: the window is the joined line's overflow, not what the continuation line saves: behind
    ///     <c>internal sealed class</c> the oracle breaks the name at 124 and not at 125.
    /// </summary>
    [Fact]
    public void TheNameBreaksOnlyUpTo124Columns_WhateverTheHead() {
        Agrees(
            $$"""
              internal sealed class Nnnnnnnnnn : IAlphaInterfaceNameValue, IGamma{{R('g', 53)}} { }
              """,
            """
            internal sealed class
                Nnnnnnnnnn : IAlphaInterfaceNameValue, IGammaggggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """
        );
        Agrees(
            $$"""
              internal sealed class Nnnnnnnnnn : IAlphaInterfaceNameValue, IGamma{{R('g', 54)}} { }
              """,
            """
            internal sealed class Nnnnnnnnnn : IAlphaInterfaceNameValue,
                IGammagggggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """
        );
    }

    /// <summary>
    ///     #539 round three: <c>9·column + 6·name − 3·first item + 807 ≥ 8·end</c>: an 18-letter name behind
    ///     <c>public class</c> stays, a 20-letter one breaks at 122 and not at 123, an 8-letter one behind
    ///     <c>public sealed class</c> breaks at 121, and three interfaces answer as two.
    /// </summary>
    [Fact]
    public void AShortNameBehindAShortHead_StaysAndTheListWraps() {
        Agrees(
            $$"""
              public class Nnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue, IGamma{{R('g', 51)}} { }
              """,
            """
            public class Nnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue,
                IGammaggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """
        );
        Agrees(
            $$"""
              public class Nnnnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue, IGamma{{R('g', 50)}} { }
              """,
            $$"""
              public class
                  Nnnnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue, IGamma{{R('g', 50)}} { }
              """
        );
        Agrees(
            $$"""
              public class Nnnnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue, IGamma{{R('g', 51)}} { }
              """,
            """
            public class Nnnnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue,
                IGammaggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """
        );
        Agrees(
            $$"""
              public sealed class Nnnnnnnn : IAlphaInterfaceNameValue, IGamma{{R('g', 54)}} { }
              """,
            """
            public sealed class
                Nnnnnnnn : IAlphaInterfaceNameValue, IGammagggggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """
        );
        Agrees(
            $$"""
              public sealed class Nnnnnnnn : IAlphaInterfaceNameValue, IGamma{{R('g', 55)}} { }
              """,
            """
            public sealed class Nnnnnnnn : IAlphaInterfaceNameValue,
                IGammaggggggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """
        );
        Agrees(
            $$"""
              public class N{{R('n', 17)}} : IAlphaInterfaceNameValue, IBetaInterfaceNameValue, IGamma{{R('g', 26)}} { }
              """,
            """
            public class Nnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue,
                IBetaInterfaceNameValue,
                IGammagggggggggggggggggggggggggg { }
            """
        );
    }

    /// <summary>
    ///     #539 round three: a 10-letter name breaks at 121 before a 4-letter first base type, a 14-letter one does not before
    ///     a 20-letter one, and a first base type past 22 letters stops counting.
    /// </summary>
    [Fact]
    public void TheFirstBaseTypesWidth_MovesTheThreshold() {
        Agrees(
            $$"""
              public class Nnnnnnnnnn : Ifff, IGamma{{R('g', 79)}} { }
              """,
            $$"""
              public class
                  Nnnnnnnnnn : Ifff, IGamma{{R('g', 79)}} { }
              """
        );
        Agrees(
            $$"""
              public class Nnnnnnnnnnnnnn : Ifffffffffffffffffff, IGamma{{R('g', 59)}} { }
              """,
            """
            public class Nnnnnnnnnnnnnn : Ifffffffffffffffffff,
                IGammaggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg { }
            """
        );
        Agrees(
            $$"""
              public class Nnnnnnnnnnnnnnnnnnnnnn : Ifffffffffffffffffffffffffffffffffff, IGamma{{R('g', 37)}} { }
              """,
            $$"""
              public class
                  Nnnnnnnnnnnnnnnnnnnnnn : Ifffffffffffffffffffffffffffffffffff, IGamma{{R('g', 37)}} { }
              """
        );
        Agrees(
            $$"""
              public class Nnnnnnnnnnnnnnnnnn : Ifffffffffffffffffffffffffffffffffff, IGamma{{R('g', 39)}} { }
              """,
            """
            public class Nnnnnnnnnnnnnnnnnn : Ifffffffffffffffffffffffffffffffffff,
                IGammaggggggggggggggggggggggggggggggggggggggg { }
            """
        );
    }

    /// <summary>
    ///     #539 round three: with two type parameters and no base list the list's comma competes the same way, from its own
    ///     constant: a 25-letter name stays at 121, a 30-letter one breaks to 123.
    /// </summary>
    [Fact]
    public void TwoTypeParameters_TheListsCommaTakesAShortName() {
        Agrees(
            $$"""
              public class NNNNNNNNNNNNNNNNNNNNNNNNN<TFirstParameterName, TSecond{{R('x', 49)}}> { }
              """,
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNN<TFirstParameterName,
                TSecondxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """
        );
        Agrees(
            $$"""
              public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirstParameterName, TSecond{{R('x', 46)}}> { }
              """,
            $$"""
              public class
                  NNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirstParameterName, TSecond{{R('x', 46)}}> { }
              """
        );
        Agrees(
            $$"""
              public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirstParameterName, TSecond{{R('x', 47)}}> { }
              """,
            """
            public class NNNNNNNNNNNNNNNNNNNNNNNNNNNNNN<TFirstParameterName,
                TSecondxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> { }
            """
        );
    }

    /// <summary>
    ///     #539 round three: behind <c>class</c> alone the name never moves down for the base list's sake, but it does when
    ///     the name itself runs past the margin, and before a lone base type.
    /// </summary>
    [Fact]
    public void ABareKeyword_KeepsTheNameForAList_ButNotForALoneBaseType() {
        Agrees(
            $$"""
              class N{{R('n', 39)}} : IAlphaInterfaceNameValue, IGammaggggggggggggggggggggggggggggggggggggg { }
              """,
            """
            class Nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue,
                IGammaggggggggggggggggggggggggggggggggggggg { }
            """
        );
        Agrees(
            $$"""
              class {{R('N', 104)}} : IFoo { }
              """,
            $$"""
              class
                  {{R('N', 104)}} : IFoo { }
              """
        );
        Agrees(
            $$"""
              class {{R('N', 114)}} { }
              """,
            $$"""
              class
                  {{R('N', 114)}} { }
              """
        );
    }

    /// <summary>
    ///     #539 round three: the oracle measures the header as if <c>{ }</c> ended it: with a member inside, the name breaks
    ///     while the line through the <c>{</c> is 122 columns and not at 123.
    /// </summary>
    [Fact]
    public void ABodyBelowTheBrace_CountsAsIfItsBraceFollowed() {
        Agrees(
            $$"""
              public class Nnnnnnnnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue, IGamma{{R('g', 48)}} {
                  int x;
              }
              """,
            $$"""
              public class
                  Nnnnnnnnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue, IGamma{{R('g', 48)}} {
                  int x;
              }
              """
        );
        Agrees(
            $$"""
              public class Nnnnnnnnnnnnnnnnnnnnnnnn : IAlphaInterfaceNameValue, IGamma{{R('g', 49)}} {
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
            $$"""
              public abstract partial class Nnnnnnnnnn : IAlphaInterfaceNameValue, IGamma{{R('g', 46)}} {
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

    /// <summary>
    ///     #539 round three: before a lone base type the name moves down whenever the line is too long, behind a type
    ///     parameter list too (Serilog's <c>LogEventPropertyValueRewriter</c>), and the <c>:</c> breaks as well when that is
    ///     not enough.
    /// </summary>
    [Fact]
    public void ALoneBaseType_TakesTheNameBreakAtAnyWidth() {
        Agrees(
            $$"""
              {{Long2}}
                  int x;
              }
              """,
            """
            public abstract class
                LogEventPropertyValueRewriter<TState> : LogEventPropertyValueVisitor<TState, Lzzzzzzzzzzzzzzzzzzzzz> {
                int x;
            }
            """
        );
        Agrees(
            $$"""
              public abstract class LogEventPropertyValueRewriterXXXXXXX : LogEventPropertyValueVisitorB{{R('z', 38)}} {
                  int x;
              }
              """,
            $$"""
              public abstract class
                  LogEventPropertyValueRewriterXXXXXXX : LogEventPropertyValueVisitorB{{R('z', 38)}} {
                  int x;
              }
              """
        );
        Agrees(
            $$"""
              public abstract class LogEventPropertyValueRewriterXXXXXXX : LogEventPropertyValueVisitorB{{R('z', 47)}} {
                  int x;
              }
              """,
            """
            public abstract class
                LogEventPropertyValueRewriterXXXXXXX :
                LogEventPropertyValueVisitorBzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz {
                int x;
            }
            """
        );
    }

    /// <summary>
    ///     #539 round three: behind <c>class</c> alone a lone base type does not widen the window: a 72-letter name breaks at
    ///     124 and not at 126, where behind <c>public class</c> it still breaks at 150.
    /// </summary>
    [Fact]
    public void ALoneBaseTypeBehindABareKeyword_KeepsTheOrdinaryWindow() {
        Agrees(
            $$"""
              class N{{R('n', 71)}} : SomeVeryLongBasebbbbbbbbbbbbbbbbbbbbbbb { }
              """,
            """
            class
                Nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn :
                SomeVeryLongBasebbbbbbbbbbbbbbbbbbbbbbb { }
            """
        );
        Agrees(
            $$"""
              class N{{R('n', 71)}} : SomeVeryLongBasebbbbbbbbbbbbbbbbbbbbbbbbb { }
              """,
            """
            class Nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn :
                SomeVeryLongBasebbbbbbbbbbbbbbbbbbbbbbbbb { }
            """
        );
        Agrees(
            $$"""
              public class N{{R('n', 71)}} : SomeVeryLongBasebbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb { }
              """,
            """
            public class
                Nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn :
                SomeVeryLongBasebbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb { }
            """
        );
    }
}
