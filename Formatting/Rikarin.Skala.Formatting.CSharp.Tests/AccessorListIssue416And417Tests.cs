using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issues #416 and #417, SK-DIV-0096 and SK-DIV-0183: an accessor list is on its owner's line or one
///     accessor per line, decided by what its accessors are and never by where the author broke it. Under
///     the export a bodiless list is joined and a list with any body is expanded; under
///     <c>skala_keep_existing_declaration_block_arrangement = true</c> a one-line list stays and a broken
///     one gets one accessor per line. Every expected string is <c>jb cleanupcode</c>'s own output for the
///     input, and each test asserts the second pass too.
/// </summary>
/// <remarks>
///     The committed fixtures are <c>constructs/breaks/accessor-lists.cs</c> and, under the declaration
///     block key, <c>constructs/preservation/accessor-lists.cs</c>.
/// </remarks>
public sealed class AccessorListIssue416And417Tests {
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

    /// <summary>
    ///     Issue #416: an expression-bodied accessor, a block-bodied one, a body beside a bodiless one, an indexer's
    ///     and an event's <c>add</c>/<c>remove</c> each put every accessor on its own line under the export.
    /// </summary>
    [Fact]
    public void AnAccessorWithABody_PutsEveryAccessorOnItsOwnLine() =>
        Agrees(
            """
            using System;

            class C {
                int _p;

                int Expr { get => _p; set => _p = value; }

                int ExprGet { get => 1; }

                int Block { get { return 1; } }

                int Blocks { get { return _p; } set { _p = value; } }

                int Mixed { get; set { } }

                int Mixed2 { get => 1; set; }

                int this[int i] { get => i; set { } }

                event EventHandler E { add { } remove { } }
            }
            """,
            """
            using System;

            class C {
                int _p;

                int Expr {
                    get => _p;
                    set => _p = value;
                }

                int ExprGet {
                    get => 1;
                }

                int Block {
                    get { return 1; }
                }

                int Blocks {
                    get { return _p; }
                    set { _p = value; }
                }

                int Mixed {
                    get;
                    set { }
                }

                int Mixed2 {
                    get => 1;
                    set;
                }

                int this[int i] {
                    get => i;
                    set { }
                }

                event EventHandler E {
                    add { }
                    remove { }
                }
            }
            """
        );

    /// <summary>
    ///     Issue #416's own example, verbatim: four one-line accessor-list properties, each expanded, with
    ///     <c>blank_lines_around_property = 1</c> between them.
    /// </summary>
    [Fact]
    public void TheIssue416Example_IsExpandedWithTheMultiLinePropertysBlankLines() =>
        Agrees(
            """
            class C {
                int _n;
                public int X { get => 1; }
                public int Y { get => _n; }
                int Z { get => 1; }
                public int W { get => 1; set { } }
            }
            """,
            """
            class C {
                int _n;

                public int X {
                    get => 1;
                }

                public int Y {
                    get => _n;
                }

                int Z {
                    get => 1;
                }

                public int W {
                    get => 1;
                    set { }
                }
            }
            """
        );

    /// <summary>
    ///     Issue #417: a bodiless accessor list written over lines — one accessor per line, all on one inner line, the
    ///     first on the brace's line — is joined, with <c>private set</c>, <c>init</c>, <c>required</c>,
    ///     <c>readonly get</c>, an initializer, an abstract indexer and an interface's members alike.
    /// </summary>
    [Fact]
    public void ABodilessAccessorList_IsJoinedHoweverItWasBroken() =>
        Agrees(
            """
            abstract class C {
                int B {
                    get;
                    set;
                }

                public int D {
                    get; set;
                }

                public int E { get;
                    private set;
                }

                public required int F {
                    get;
                    init;
                }

                int G {
                    get;
                    set;
                } = 1;

                public abstract int this[int i] {
                    get;
                    set;
                }

                protected abstract int H {
                    get;
                }
            }

            struct S {
                public int Y {
                    readonly get;
                    set;
                }
            }

            interface I {
                int K {
                    get;
                }

                int this[string s] {
                    get;
                    set;
                }
            }
            """,
            """
            abstract class C {
                int B { get; set; }

                public int D { get; set; }

                public int E { get; private set; }

                public required int F { get; init; }

                int G { get; set; } = 1;

                public abstract int this[int i] { get; set; }

                protected abstract int H { get; }
            }

            struct S {
                public int Y { readonly get; set; }
            }

            interface I {
                int K { get; }

                int this[string s] { get; set; }
            }
            """
        );

    /// <summary>
    ///     The margin, two indents deep: a joined list at 120 columns stays, one at 121 is expanded whether it was
    ///     written joined or broken, and an initializer is not part of the measure — the list stays joined and the
    ///     <c>=</c> wraps.
    /// </summary>
    [Fact]
    public void TheMargin_ExpandsAJoinedListAndNotTheInitializer() =>
        Agrees(
            """
            using System.Collections.Generic;

            namespace N {
                class Outer {
                    class C {
                        int _p;

                        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123456789Abcdefg { get; private set; }

                        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123456789AbcdefgHIJKLM { get; private set; }

                        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123456789AbcdefgHIJKLMN {
                            get;
                            private set;
                        }

                        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123456789AbcdefgHIJKLMO { get; private set; }

                        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123456789AbcdefghijklmNOP { get; private set; }

                        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123456789AbcdefghijklmNOQ {
                            get;
                            private set;
                        }

                        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz012345 {
                            get;
                            private set;
                        }

                        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123 { get; set; } = new Dictionary<string, List<int>>();

                        public Dictionary<string, List<int>> Abcdefghijkl {
                            get;
                            set;
                        } = new Dictionary<string, List<int>>();
                    }
                }
            }
            """,
            """
            using System.Collections.Generic;

            namespace N {
                class Outer {
                    class C {
                        int _p;

                        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123456789Abcdefg { get; private set; }

                        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123456789AbcdefgHIJKLM { get; private set; }

                        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123456789AbcdefgHIJKLMN {
                            get;
                            private set;
                        }

                        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123456789AbcdefgHIJKLMO {
                            get;
                            private set;
                        }

                        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123456789AbcdefghijklmNOP {
                            get;
                            private set;
                        }

                        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123456789AbcdefghijklmNOQ {
                            get;
                            private set;
                        }

                        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz012345 { get; private set; }

                        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123 { get; set; } =
                            new Dictionary<string, List<int>>();

                        public Dictionary<string, List<int>> Abcdefghijkl { get; set; } = new Dictionary<string, List<int>>();
                    }
                }
            }
            """
        );

    /// <summary>
    ///     A line comment inside a bodiless list, or a comment on a line of its own, keeps it expanded; a block comment
    ///     on a joined list stays; a list with a body is expanded around its comment, and a comment after the closing
    ///     brace is not inside.
    /// </summary>
    [Fact]
    public void ALineComment_KeepsABodilessListExpanded_AndABodiedOneIsExpandedAroundIt() =>
        Agrees(
            """
            class C {
                int Line {
                    get; // why
                    set;
                }

                int Block { get; /* c */ set; }

                int Lead {
                    // lead
                    get;
                    set;
                }

                int Trailing {
                    get;
                    set;
                } // trailing

                int ExprBlock { get => 1; /* c */ }

                int ExprLine { get => 1; // c
                }
            }
            """,
            """
            class C {
                int Line {
                    get; // why
                    set;
                }

                int Block { get; /* c */ set; }

                int Lead {
                    // lead
                    get;
                    set;
                }

                int Trailing { get; set; } // trailing

                int ExprBlock {
                    get => 1; /* c */
                }

                int ExprLine {
                    get => 1; // c
                }
            }
            """
        );

    /// <summary>
    ///     A block comment that ends a line is not a reason to keep a bodiless list broken: <c>get; /* c */</c>,
    ///     <c>{ /* c */</c> and <c>set; /* c */</c> each join, a comment on a line of its own stays there, a line
    ///     comment expands a joined list, and a directive inside the list leaves it alone.
    /// </summary>
    [Fact]
    public void ABlockCommentThatEndsALine_JoinsWithIt() =>
        Agrees(
            """
            class C {
                int A {
                    get; /* c */
                    set;
                }

                int B { /* c */
                    get;
                    set;
                }

                int D {
                    /* c */ get;
                    set;
                }

                int E {
                    get;
                    set; /* c */
                }

                int F {
                    get;
                    set;
                    /* c */
                }

                int H { get; set; // c
                }

            #if X
                int I {
                    get;
                    set;
                }
            #endif

                int J {
            #if X
                    get;
            #endif
                    set;
                }
            }
            """,
            """
            class C {
                int A { get; /* c */ set; }

                int B { /* c */ get; set; }

                int D {
                    /* c */
                    get;
                    set;
                }

                int E { get; set; /* c */ }

                int F {
                    get;
                    set;
                    /* c */
                }

                int H {
                    get;
                    set; // c
                }

            #if X
                int I {
                    get;
                    set;
                }
            #endif

                int J {
            #if X
                    get;
            #endif
                    set;
                }
            }
            """
        );

    /// <summary>
    ///     Under <c>skala_keep_existing_declaration_block_arrangement = true</c> the same input keeps every broken list
    ///     one accessor per line, a block comment at a line's end included.
    /// </summary>
    [Fact]
    public void UnderTheDeclarationBlockKey_ACommentedBrokenListStaysBroken() =>
        Agrees(
            """
            class C {
                int A {
                    get; /* c */
                    set;
                }

                int B { /* c */
                    get;
                    set;
                }

                int D {
                    /* c */ get;
                    set;
                }

                int E {
                    get;
                    set; /* c */
                }

                int F {
                    get;
                    set;
                    /* c */
                }

                int H { get; set; // c
                }

            #if X
                int I {
                    get;
                    set;
                }
            #endif

                int J {
            #if X
                    get;
            #endif
                    set;
                }
            }
            """,
            """
            class C {
                int A {
                    get; /* c */
                    set;
                }

                int B { /* c */
                    get;
                    set;
                }

                int D {
                    /* c */
                    get;
                    set;
                }

                int E {
                    get;
                    set; /* c */
                }

                int F {
                    get;
                    set;
                    /* c */
                }

                int H {
                    get;
                    set; // c
                }

            #if X
                int I {
                    get;
                    set;
                }
            #endif

                int J {
            #if X
                    get;
            #endif
                    set;
                }
            }
            """,
            ("skala_keep_existing_declaration_block_arrangement", "true")
        );

    /// <summary>
    ///     The export's <c>place_accessor_attribute_on_same_line</c> puts <c>[Obsolete]</c> on a line of its own, which
    ///     breaks a bodiless list; a property's own attribute is not inside the list, which is joined below it.
    /// </summary>
    [Fact]
    public void AnAccessorAttributeOnItsOwnLine_BreaksTheList() =>
        Agrees(
            """
            using System;

            class C {
                int A { [Obsolete] get; set; }

                int B {
                    [Obsolete] get => 1;
                }

                [Obsolete] int D {
                    get;
                    set;
                }
            }
            """,
            """
            using System;

            class C {
                int A {
                    [Obsolete]
                    get;
                    set;
                }

                int B {
                    [Obsolete]
                    get => 1;
                }

                [Obsolete]
                int D { get; set; }
            }
            """
        );

    /// <summary>
    ///     At <c>skala_place_accessor_attribute_on_same_line = always</c> the same input joins
    ///     <c>{ [Obsolete] get; set; }</c>.
    /// </summary>
    [Fact]
    public void AnAccessorAttributeKeptOnTheLine_LetsTheListJoin() =>
        Agrees(
            """
            using System;

            class C {
                int A { [Obsolete] get; set; }

                int B {
                    [Obsolete] get => 1;
                }

                [Obsolete] int D {
                    get;
                    set;
                }
            }
            """,
            """
            using System;

            class C {
                int A { [Obsolete] get; set; }

                int B {
                    [Obsolete] get => 1;
                }

                [Obsolete]
                int D { get; set; }
            }
            """,
            ("skala_place_accessor_attribute_on_same_line", "always")
        );

    /// <summary>
    ///     Under <c>skala_keep_existing_declaration_block_arrangement = true</c> what the accessors are does not
    ///     matter: a list written on one line stays on it unless the margin or an attribute breaks it, and one written
    ///     over lines — <c>{</c>↵<c>get; set;</c>↵<c>}</c> included — gets one accessor per line.
    /// </summary>
    [Fact]
    public void UnderTheDeclarationBlockKey_AOneLineListStays_AndABrokenOneGetsOneAccessorPerLine() =>
        Agrees(
            """
            using System;
            using System.Collections.Generic;

            abstract class C {
                int _p;

                int Expr { get => _p; set => _p = value; }

                int Block { get { return 1; } }

                int Mixed { get; set { } }

                event EventHandler E { add { } remove { } }

                int Auto { get; set; }

                int Inner {
                    get; set;
                }

                int Half { get;
                    set;
                }

                int Each {
                    get;
                    set;
                }

                int ExprBroken { get => _p;
                    set => _p = value; }

                int Attr { [Obsolete] get; set; }

                public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123456789AbcdefghijklmNOPQRSTUVWXYZ { get; set; }

                public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123456789AbcdefghijklmNOPQRSTUVWXYZ0 { get => null; }
            }
            """,
            """
            using System;
            using System.Collections.Generic;

            abstract class C {
                int _p;

                int Expr { get => _p; set => _p = value; }

                int Block { get { return 1; } }

                int Mixed { get; set { } }

                event EventHandler E { add { } remove { } }

                int Auto { get; set; }

                int Inner {
                    get;
                    set;
                }

                int Half {
                    get;
                    set;
                }

                int Each {
                    get;
                    set;
                }

                int ExprBroken {
                    get => _p;
                    set => _p = value;
                }

                int Attr {
                    [Obsolete]
                    get;
                    set;
                }

                public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123456789AbcdefghijklmNOPQRSTUVWXYZ { get; set; }

                public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123456789AbcdefghijklmNOPQRSTUVWXYZ0 {
                    get => null;
                }
            }
            """,
            ("skala_keep_existing_declaration_block_arrangement", "true")
        );
}
