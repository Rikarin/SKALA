using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #434, SK-DIV-0199: a block comment after a declaration's last attribute section leaves the
///     gap to the author. <c>[Obsolete] /* c */ public void M() { }</c> stays on one line and
///     <c>[Obsolete] /* c */</c> / <c>public void M() { }</c> stays on two, under every value of the
///     <c>place_*_attribute_on_same_line</c> keys and with the arrangement kept; #409's point that survives
///     a comment used to carry the placement break past it. Between two sections the point still
///     survives. Every expected string is <c>jb cleanupcode</c>'s own output for the input, and a second
///     pass is asserted.
/// </summary>
public sealed class AttributeCommentIssue434Tests {
    const string A20 = "aaaaaaaaaaaaaaaaaaaa";

    const string B101 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
        + "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    const string B26 = "bbbbbbbbbbbbbbbbbbbbbbbbbb";

    const string Members =
        $$"""
        public class In {
            [Obsolete] /* c */ public void M1() { }
            [Obsolete] /* c */ public void M1b() { int x = 1; Use(x); }
            [Obsolete] /* c */ public int F1;
            public int P1 { [Obsolete] /* c */ get; set; }
            [Obsolete] /* c */ [Serializable] public void M2() { }
            [Obsolete] [Serializable] /* c */ public void M3() { }
            [Obsolete] /* c
               d */ public void M4() { }
            [Obsolete] /* c */
            public void M6() { }
            [Obsolete]
            /* c */ public void M7() { }
            [Obsolete]
            /* c */
            public void M7b() { }
            [Obsolete] /* c */
            /* d */ public void M7c() { }
            [Obsolete] /* c */ public class N1 { }
            [Obsolete] /* c */ public int P2 { get; set; }
            [Obsolete] /* c */ public event System.Action E1;
            [Obsolete] /* c */ public In() { }
            [Obsolete] /* c */ public void LongMethodName(int {{A20}}, int {{B26}}, int ccccccccccc) { }
            [Obsolete] /* c */ public void LongMethodNam2(int {{A20}}, int {{B101}}) { }
            void L() {
                [Obsolete] /* c */ void Local() { }
                Local();
            }
        }

        public record R([Obsolete] /* c */ int A, [property: Obsolete] /* c */ int B);
        """;

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

    static (string Key, string Value)[] Every(string value) => [
        ("skala_place_method_attribute_on_same_line", value),
        ("skala_place_type_attribute_on_same_line", value),
        ("skala_place_field_attribute_on_same_line", value),
        ("skala_place_accessorholder_attribute_on_same_line", value),
        ("skala_place_accessor_attribute_on_same_line", value),
        ("skala_place_record_field_attribute_on_same_line", value)
    ];

    /// <summary>
    ///     The export's <c>never</c>: a comment after the last section keeps the declaration where the
    ///     author put it — on the attribute's line, below it, or below a comment on a line of its own —
    ///     and a declaration too long for the line chops its parameters with the attribute still on it.
    ///     Between <c>[Obsolete] /* c */</c> and <c>[Serializable]</c> the break is still taken.
    /// </summary>
    [Fact]
    public void UnderNever_TheCommentKeepsTheAuthorsLines() =>
        Agrees(
            Members,
            $$"""
            public class In {
                [Obsolete] /* c */ public void M1() { }

                [Obsolete] /* c */ public void M1b() {
                    int x = 1;
                    Use(x);
                }

                [Obsolete] /* c */ public int F1;
                public int P1 { [Obsolete] /* c */ get; set; }

                [Obsolete] /* c */
                [Serializable]
                public void M2() { }

                [Obsolete]
                [Serializable] /* c */ public void M3() { }

                [Obsolete] /* c
                   d */ public void M4() { }

                [Obsolete] /* c */
                public void M6() { }

                [Obsolete]
                /* c */ public void M7() { }

                [Obsolete]
                /* c */
                public void M7b() { }

                [Obsolete] /* c */
                /* d */ public void M7c() { }

                [Obsolete] /* c */ public class N1 { }

                [Obsolete] /* c */ public int P2 { get; set; }
                [Obsolete] /* c */ public event System.Action E1;
                [Obsolete] /* c */ public In() { }

                [Obsolete] /* c */ public void LongMethodName(
                    int {{A20}},
                    int {{B26}},
                    int ccccccccccc
                ) { }

                [Obsolete] /* c */ public void LongMethodNam2(
                    int {{A20}},
                    int {{B101}}
                ) { }

                void L() {
                    [Obsolete] /* c */ void Local() { }
                    Local();
                }
            }

            public record R([Obsolete] /* c */ int A, [property: Obsolete] /* c */ int B);
            """
        );

    /// <summary>
    ///     <c>always</c> joins the two sections around a comment and still leaves the gap after the last
    ///     one alone: <c>[Obsolete] /* c */</c> / <c>public void M6() { }</c> stays on two lines.
    /// </summary>
    [Fact]
    public void UnderAlways_TheGapAfterTheCommentIsStillTheAuthors() =>
        Agrees(
            Members,
            $$"""
            public class In {
                [Obsolete] /* c */ public void M1() { }

                [Obsolete] /* c */ public void M1b() {
                    int x = 1;
                    Use(x);
                }

                [Obsolete] /* c */ public int F1;
                public int P1 { [Obsolete] /* c */ get; set; }
                [Obsolete] /* c */ [Serializable] public void M2() { }
                [Obsolete] [Serializable] /* c */ public void M3() { }

                [Obsolete] /* c
                   d */ public void M4() { }

                [Obsolete] /* c */
                public void M6() { }

                [Obsolete]
                /* c */ public void M7() { }

                [Obsolete]
                /* c */
                public void M7b() { }

                [Obsolete] /* c */
                /* d */ public void M7c() { }

                [Obsolete] /* c */ public class N1 { }

                [Obsolete] /* c */ public int P2 { get; set; }
                [Obsolete] /* c */ public event System.Action E1;
                [Obsolete] /* c */ public In() { }

                [Obsolete] /* c */ public void LongMethodName(
                    int {{A20}},
                    int {{B26}},
                    int ccccccccccc
                ) { }

                [Obsolete] /* c */ public void LongMethodNam2(
                    int {{A20}},
                    int {{B101}}
                ) { }

                void L() {
                    [Obsolete] /* c */ void Local() { }
                    Local();
                }
            }

            public record R([Obsolete] /* c */ int A, [property: Obsolete] /* c */ int B);
            """,
            Every("always")
        );

    [Fact]
    public void UnderIfOwnerIsSingleLine_TheGapAfterTheCommentIsStillTheAuthors() =>
        Agrees(
            Members,
            $$"""
            public class In {
                [Obsolete] /* c */ public void M1() { }

                [Obsolete] /* c */ public void M1b() {
                    int x = 1;
                    Use(x);
                }

                [Obsolete] /* c */ public int F1;
                public int P1 { [Obsolete] /* c */ get; set; }
                [Obsolete] /* c */ [Serializable] public void M2() { }
                [Obsolete] [Serializable] /* c */ public void M3() { }

                [Obsolete] /* c
                   d */ public void M4() { }

                [Obsolete] /* c */
                public void M6() { }

                [Obsolete]
                /* c */ public void M7() { }

                [Obsolete]
                /* c */
                public void M7b() { }

                [Obsolete] /* c */
                /* d */ public void M7c() { }

                [Obsolete] /* c */ public class N1 { }

                [Obsolete] /* c */ public int P2 { get; set; }
                [Obsolete] /* c */ public event System.Action E1;
                [Obsolete] /* c */ public In() { }

                [Obsolete] /* c */ public void LongMethodName(
                    int {{A20}},
                    int {{B26}},
                    int ccccccccccc
                ) { }

                [Obsolete] /* c */ public void LongMethodNam2(
                    int {{A20}},
                    int {{B101}}
                ) { }

                void L() {
                    [Obsolete] /* c */ void Local() { }
                    Local();
                }
            }

            public record R([Obsolete] /* c */ int A, [property: Obsolete] /* c */ int B);
            """,
            Every("if_owner_is_single_line")
        );

    [Fact]
    public void WithTheArrangementKept_TheGapAfterTheCommentIsStillTheAuthors() =>
        Agrees(
            Members,
            $$"""
            public class In {
                [Obsolete] /* c */ public void M1() { }

                [Obsolete] /* c */ public void M1b() {
                    int x = 1;
                    Use(x);
                }

                [Obsolete] /* c */ public int F1;
                public int P1 { [Obsolete] /* c */ get; set; }
                [Obsolete] /* c */ [Serializable] public void M2() { }
                [Obsolete] [Serializable] /* c */ public void M3() { }

                [Obsolete] /* c
                   d */ public void M4() { }

                [Obsolete] /* c */
                public void M6() { }

                [Obsolete]
                /* c */ public void M7() { }

                [Obsolete]
                /* c */
                public void M7b() { }

                [Obsolete] /* c */
                /* d */ public void M7c() { }

                [Obsolete] /* c */ public class N1 { }

                [Obsolete] /* c */ public int P2 { get; set; }
                [Obsolete] /* c */ public event System.Action E1;
                [Obsolete] /* c */ public In() { }

                [Obsolete] /* c */ public void LongMethodName(
                    int {{A20}},
                    int {{B26}},
                    int ccccccccccc
                ) { }

                [Obsolete] /* c */ public void LongMethodNam2(
                    int {{A20}},
                    int {{B101}}
                ) { }

                void L() {
                    [Obsolete] /* c */ void Local() { }
                    Local();
                }
            }

            public record R([Obsolete] /* c */ int A, [property: Obsolete] /* c */ int B);
            """,
            ("skala_keep_existing_attribute_arrangement", "true")
        );

    /// <summary>
    ///     The other owners — delegate, interface and its member, struct, indexer, operator, destructor,
    ///     an event with accessors, an accessor in a one-line list, a <c>return:</c> target — and the top
    ///     level, where <c>[assembly: A] /* c */ [assembly: B]</c> is a gap between sections and breaks.
    /// </summary>
    [Fact]
    public void EveryOwner_KeepsItsDeclarationAfterTheComment() =>
        Agrees(
            """
            [assembly: A] /* c */ [assembly: B]
            [assembly: C] /* c */
            namespace N {
                [Obsolete] /* c */ public delegate void D();
                [Obsolete] /* c */ public interface I { [Obsolete] /* c */ void M(); }
                [Obsolete] /* c */ public struct S { }
                public class K {
                    [Obsolete] /* c */ public int this[int i] => i;
                    [Obsolete] /* c */ public static K operator +(K a, K b) => a;
                    [Obsolete] /* c */ ~K() { }
                    [Obsolete] /* c */ public event System.Action E { add { } remove { } }
                    public int P { get; [Obsolete] /* c */ set; }
                    [return: Obsolete] /* c */ public int R() => 1;
                }
            }
            """,
            """
            [assembly: A] /* c */
            [assembly: B]
            [assembly: C] /* c */

            namespace N {
                [Obsolete] /* c */ public delegate void D();

                [Obsolete] /* c */ public interface I {
                    [Obsolete] /* c */ void M();
                }

                [Obsolete] /* c */ public struct S { }

                public class K {
                    [Obsolete] /* c */ public int this[int i] => i;
                    [Obsolete] /* c */ public static K operator +(K a, K b) => a;
                    [Obsolete] /* c */ ~K() { }

                    [Obsolete] /* c */ public event System.Action E {
                        add { }
                        remove { }
                    }

                    public int P { get; [Obsolete] /* c */ set; }
                    [return: Obsolete] /* c */ public int R() => 1;
                }
            }
            """
        );

    /// <summary>
    ///     The issue's top-level case: <c>[Obsolete] /* c */ public class G { }</c> stays one line, and
    ///     <c>[Obsolete] /* c */ [Serializable] public class G3 { }</c> breaks after the comment and after
    ///     <c>[Serializable]</c>, because only the second gap follows the last section.
    /// </summary>
    [Fact]
    public void TheIssuesCase_AtTheTopLevel() =>
        Agrees(
            """
            [Obsolete] /* c */ public class G { }
            [Obsolete] /* c */ [Serializable] public class G3 { }
            [Obsolete] [Serializable] /* c */ public class G4 { }
            [Obsolete] /* c
              d */ public class G5 { }
            [Obsolete] /** c */ public class G6 { }
            [Obsolete] /* c */
            public class G7 { }
            [Obsolete] /* c */ public class G8 { [Obsolete] /* c */ public class N2 { } }
            """,
            """
            [Obsolete] /* c */ public class G { }

            [Obsolete] /* c */
            [Serializable]
            public class G3 { }

            [Obsolete]
            [Serializable] /* c */ public class G4 { }

            [Obsolete] /* c
              d */ public class G5 { }

            [Obsolete] /** c */ public class G6 { }

            [Obsolete] /* c */
            public class G7 { }

            [Obsolete] /* c */ public class G8 {
                [Obsolete] /* c */ public class N2 { }
            }
            """
        );
}
