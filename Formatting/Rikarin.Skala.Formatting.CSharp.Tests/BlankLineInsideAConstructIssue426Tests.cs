using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #426, SK-DIV-0196: a blank line inside a construct is removed whatever the keep keys say, and
///     the comment the issue saw it after was incidental. Every expected string is <c>jb cleanupcode</c>
///     2025.2.6's own output for the input, and each test asserts the second pass too.
/// </summary>
public sealed class BlankLineInsideAConstructIssue426Tests {
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
    ///     Issue #426's three shapes and the closers <c>)</c>, <c>]</c>, <c>}</c> of an initializer and <c>&gt;</c>,
    ///     after a line and a block comment; a block's <c>}</c> is the near-brace key's.
    /// </summary>
    [Fact]
    public void TheIssuesShapes_LoseTheBlankBeforeTheCloser() =>
        Agrees(
            """
            class K {
                void A() {
                    M(1, 2

                    );
                    M(1, 2 // e

                    );
                    M(1,
                        2 /*e*/

                    );
                    M(1, 2 /*e*/

                    );
                    var a = new[] { 1, 2 // e

                    };
                    var b = new int[] { 1,
                        2 /*e*/

                    };
                    var x = arr[1, 2 // e

                    ];
                    var y = arr[1,
                        2 /*e*/

                    ];
                    var l = new List<int> { 1, 2 // e

                    };
                    Foo<int, string // e

                    >();
                    Foo<int,
                        string /*e*/

                    >();
                    if (a) {
                        M(); // e

                    }
                    if (a) {
                        M(); /*e*/

                    }
                }

                void B(int a, int b // e

                ) {
                }

                void C(int a,
                    int b /*e*/

                ) {
                }

                [Attr(1, 2 // e

                )]
                void D() {
                }

                int E => F(1, 2 // e

                );
            }
            """,
            """
            class K {
                void A() {
                    M(1, 2);
                    M(
                        1,
                        2 // e
                    );
                    M(
                        1,
                        2 /*e*/
                    );
                    M(
                        1,
                        2 /*e*/
                    );
                    var a = new[] {
                        1, 2 // e
                    };
                    var b = new int[] { 1, 2 /*e*/ };
                    var x = arr[1,
                        2 // e
                    ];
                    var y = arr[1,
                        2 /*e*/
                    ];
                    var l = new List<int> {
                        1, 2 // e
                    };
                    Foo<int, string // e
                    >();
                    Foo<int,
                        string /*e*/
                    >();
                    if (a) {
                        M(); // e
                    }

                    if (a) {
                        M(); /*e*/
                    }
                }

                void B(
                    int a,
                    int b // e
                ) { }

                void C(
                    int a,
                    int b /*e*/
                ) { }

                [Attr(
                    1,
                    2 // e
                )]
                void D() { }

                int E =>
                    F(
                        1,
                        2 // e
                    );
            }
            """
        );

    /// <summary>
    ///     A blank between items, after <c>(</c> and before <c>)</c> in a chopped list, with and without a comment, in an
    ///     argument list, a parameter list, a primary constructor and an enum.
    /// </summary>
    [Fact]
    public void ArgumentAndParameterLists_LoseEveryBlank() =>
        Agrees(
            """
            class K {
                void A() {
                    Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm(aaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccccccc

                    );
                    Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm(aaaaaaaaaaaaaaaaaaaaaaaa,

                        bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccccccccccccccccccccc);
                    Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm(

                        aaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccccccccccccccccccccc);
                    M(1, // e

                        2);
                    M( // e

                        1, 2);
                    M(1, /* e */

                        2);
                    var a = new[] {
                        1,
                        2

                    };
                    var b = new[] {

                        1,

                        2 };
                    var c = new[] { 1, 2 /* e */

                    };
                    Foo(x => {
                        M(); // e

                    });
                    Foo(x => x // e

                    );
                }

                void B(int a, int b /* e */

                ) {
                }

                void C(int a,

                    int b) {
                }
            }

            class D(int a, int b // e

            ) {
            }

            enum E {
                A,
                B // e

            }
            """,
            """
            class K {
                void A() {
                    Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm(
                        aaaaaaaaaaaaaaaaaaaaaaaa,
                        bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
                        ccccccccccccccccccccc
                    );
                    Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm(
                        aaaaaaaaaaaaaaaaaaaaaaaa,
                        bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
                        ccccccccccccccccccccccccccccccccccc
                    );
                    Mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm(
                        aaaaaaaaaaaaaaaaaaaaaaaa,
                        bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
                        ccccccccccccccccccccccccccccccccccc
                    );
                    M(
                        1, // e
                        2
                    );
                    M( // e
                        1,
                        2
                    );
                    M(
                        1, /* e */
                        2
                    );
                    var a = new[] { 1, 2 };
                    var b = new[] { 1, 2 };
                    var c = new[] { 1, 2 /* e */ };
                    Foo(x => {
                            M(); // e
                        }
                    );
                    Foo(x => x // e
                    );
                }

                void B(
                    int a,
                    int b /* e */
                ) { }

                void C(
                    int a,
                    int b
                ) { }
            }

            class D(
                int a,
                int b // e
            ) { }

            enum E {
                A,
                B // e
            }
            """
        );

    /// <summary>
    ///     A binary, a chain, a query, a conditional, an attribute list, constraints and a base list lose the blank; a
    ///     switch expression arm and a lambda block's statements keep it, and so does a blank before an own-line comment.
    /// </summary>
    [Fact]
    public void ConstructsWithoutComments_LoseTheBlank_SwitchArmsAndStatementsKeepIt() =>
        Agrees(
            """
            class K {
                void A() {
                    var x = aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa +

                        bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb + cccccccccccccccccccccccccccccccccccccc;
                    var y = source

                        .Select(z => z)

                        .Where(z => z);
                    var s = v switch {
                        1 => 2,

                        _ => 3
                    };
                    var o = new Ooo {
                        A = 1,

                        B = 2
                    };
                    var l = new List<int> {
                        111111111, 222222222, 333333333, 444444444, 111111111, 222222222, 333333333, 444444444, 111111111, 222222222,

                        555555555, 666666666
                    };
                    Foo(x => {
                        M();

                        N();

                    });
                    Foo(x => {
                        M();

                        N();
                    }, 2

                    );
                    var q = from a in b

                        where a

                        select a;
                    var p = cond

                        ? 1
                        : 2;
                    if (a &&

                        b) {
                    }
                    Bar(
                        1,
                        // own line

                        2);
                    Bar(
                        1,

                        // own line
                        2);
                }

                [Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,

                 Bbbbbbbbbbbbbbbbbbbbbbbbb]
                void B<T, U>()
                    where T : class

                    where U : struct {
                }
            }

            class D : Aaaaaaaaaaaaaaaaaaaaa,

                Bbbbbbbbbbbbbbbbbbbbbb {
            }
            """,
            """
            class K {
                void A() {
                    var x = aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                        + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                        + cccccccccccccccccccccccccccccccccccccc;
                    var y = source
                        .Select(z => z)
                        .Where(z => z);
                    var s = v switch {
                        1 => 2,

                        _ => 3
                    };
                    var o = new Ooo { A = 1, B = 2 };
                    var l = new List<int> {
                        111111111,
                        222222222,
                        333333333,
                        444444444,
                        111111111,
                        222222222,
                        333333333,
                        444444444,
                        111111111,
                        222222222,
                        555555555,
                        666666666
                    };
                    Foo(x => {
                            M();

                            N();
                        }
                    );
                    Foo(
                        x => {
                            M();

                            N();
                        },
                        2
                    );
                    var q = from a in b
                        where a
                        select a;
                    var p = cond
                        ? 1
                        : 2;
                    if (a && b) { }

                    Bar(
                        1,
                        // own line
                        2
                    );
                    Bar(
                        1,

                        // own line
                        2
                    );
                }

                [Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                 Bbbbbbbbbbbbbbbbbbbbbbbbb]
                void B<T, U>()
                    where T : class
                    where U : struct { }
            }

            class D : Aaaaaaaaaaaaaaaaaaaaa,
                Bbbbbbbbbbbbbbbbbbbbbb { }
            """
        );

    /// <summary>
    ///     Declarators, object, anonymous and <c>with</c> initializers, a property pattern, a <c>for</c> header and a
    ///     record's parameters lose it; accessors, case labels, enum members, a collection expression's elements and a blank
    ///     between two comments keep it.
    /// </summary>
    [Fact]
    public void InitializersDeclaratorsAndPatterns_LoseTheBlank_AccessorsLabelsAndEnumMembersKeepIt() =>
        Agrees(
            """
            class K {
                int f1 = 1,

                    f2 = 2;

                int P {
                    get => 1;

                    set { }
                }

                void A() {
                    var o = new Ooooooooooooooooooooooooooooooooooo {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,

                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333
                    };
                    var an = new {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,

                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333
                    };
                    int[] ce = [
                        111111111, 222222222, 333333333, 444444444, 111111111, 222222222, 333333333, 444444444,

                        555555555, 666666666, 111111111, 222222222, 333333333
                    ];
                    var w = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr with {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,

                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333
                    };
                    if (o is {

                        Aaaaaaaaaaaaaaaaaaaaaaaaa: 1111111111, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb: 2222222222, Cccccccccccc: 3
                    }) {
                    }
                    int a = 1,

                        b = 2;
                    for (int i = 0;

                        i < 10; i++) {
                    }
                    var s = v switch {
                        1 => 2, // c

                        _ => 3
                    };
                    var s2 = v switch {
                        1 => 2,
                        // c

                        _ => 3
                    };
                    switch (v) {
                        case 1:

                        case 2:
                            break;
                    }
                    Bar(1,
                        // c

                        // d
                        2);
                }

                void Lo(int a,
                    // c

                    int b) {
                }
            }

            enum E {
                A,

                B
            }

            record R(int A,

                int B);
            """,
            """
            class K {
                int f1 = 1,
                    f2 = 2;

                int P {
                    get => 1;

                    set { }
                }

                void A() {
                    var o = new Ooooooooooooooooooooooooooooooooooo {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333
                    };
                    var an = new {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333
                    };
                    int[] ce = [
                        111111111, 222222222, 333333333, 444444444, 111111111, 222222222, 333333333, 444444444,

                        555555555, 666666666, 111111111, 222222222, 333333333
                    ];
                    var w = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr with {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333
                    };
                    if (o is {
                            Aaaaaaaaaaaaaaaaaaaaaaaaa: 1111111111, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb: 2222222222, Cccccccccccc: 3
                        }) { }

                    int a = 1,
                        b = 2;
                    for (int i = 0;
                         i < 10;
                         i++) { }

                    var s = v switch {
                        1 => 2, // c

                        _ => 3
                    };
                    var s2 = v switch {
                        1 => 2,
                        // c

                        _ => 3
                    };
                    switch (v) {
                        case 1:

                        case 2:
                            break;
                    }

                    Bar(
                        1,
                        // c

                        // d
                        2
                    );
                }

                void Lo(
                    int a,
                    // c
                    int b
                ) { }
            }

            enum E {
                A,

                B
            }

            record R(
                int A,
                int B);
            """
        );

    /// <summary>
    ///     After a constructor initializer's comma, an attribute list, an expression body's arrow, a lambda's arrow, an
    ///     <c>=</c>, <c>return</c>, <c>await</c>, an element access's comma, and before a statement's <c>;</c>.
    /// </summary>
    [Fact]
    public void AfterTokensThatEndNoLine_TheBlankGoes() =>
        Agrees(
            """
            class K : B {
                K() : base(1,

                    2) {
                }

                [A]

                void M1() {
                }

                int P =>

                    F(1);

                void A() {
                    Func<int, int> f = x =>

                        x;
                    int a =

                        1;
                    M()

                        ;
                    return

                        a;
                    var e = arr[1,

                        2];
                    var g = await

                        x;
                }
            }
            """,
            """
            class K : B {
                K() : base(
                    1,
                    2
                ) { }

                [A]
                void M1() { }

                int P => F(1);

                void A() {
                    Func<int, int> f = x =>
                        x;
                    int a =
                        1;
                    M()
                        ;
                    return
                        a;
                    var e = arr[1,
                        2];
                    var g = await
                        x;
                }
            }
            """
        );

    /// <summary>
    ///     Two attribute lists lose the blank between them; a collection expression's <c>]</c> loses it as a block's
    ///     <c>}</c> does under <c>remove_blank_lines_near_braces_in_code</c>.
    /// </summary>
    [Fact]
    public void CollectionExpressionBrackets_AreBracesToTheNearBraceKey() =>
        Agrees(
            """
            class K {
                [A]

                [B]
                void M1() {
                }

                void A() {
                    do {
                        x();
                    }

                    while (a);
                    var s = v switch {
                        1 => 2,
                        _ => 3

                    };
                    int[] ce = [
                        1, 2, 3

                    ];
                    var l = new List<int> {
                        1, 2, 3

                    };
                    if (a) {
                        x();

                    }
                    try {
                        x();
                    }

                    catch {
                    }

                    finally {
                    }
                    Foo(x => {
                        M();

                    });
                }
            }
            """,
            """
            class K {
                [A]
                [B]
                void M1() { }

                void A() {
                    do {
                        x();
                    } while (a);

                    var s = v switch {
                        1 => 2,
                        _ => 3
                    };
                    int[] ce = [
                        1, 2, 3
                    ];
                    var l = new List<int> { 1, 2, 3 };
                    if (a) {
                        x();
                    }

                    try {
                        x();
                    } catch { } finally { }

                    Foo(x => { M(); });
                }
            }
            """
        );

    /// <summary>
    ///     After <c>[</c> and before <c>]</c> of a collection expression and before an initializer's or a pattern's
    ///     <c>}</c>, with a line comment and without.
    /// </summary>
    [Fact]
    public void AfterALineCommentInBraces_TheBlankBeforeTheBraceIsTheNearBraceKeys() =>
        Agrees(
            """
            class K {
                void A() {
                    int[] ce = [

                        1, 2, 3

                    ];
                    int[] cf = [
                        1, 2, 3 // e

                    ];
                    var l = new List<int> {
                        111111111, 222222222, 333333333, 444444444, 111111111, 222222222, 333333333, 444444444, 1, 2,
                        555555555, 666666666 // e

                    };
                    var m = new List<int> {

                        111111111, 222222222, 333333333, 444444444, 111111111, 222222222, 333333333, 444444444, 1, 2,
                        555555555, 666666666
                    };
                    if (o is {
                            Aaaaaaaaaaaaaaaaaaaaaaaaa: 1111111111, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb: 2222222222, Cccccccccccc: 3

                        }) {
                    }
                }
            }
            """,
            """
            class K {
                void A() {
                    int[] ce = [
                        1, 2, 3
                    ];
                    int[] cf = [
                        1, 2, 3 // e
                    ];
                    var l = new List<int> {
                        111111111,
                        222222222,
                        333333333,
                        444444444,
                        111111111,
                        222222222,
                        333333333,
                        444444444,
                        1,
                        2,
                        555555555,
                        666666666 // e
                    };
                    var m = new List<int> {
                        111111111,
                        222222222,
                        333333333,
                        444444444,
                        111111111,
                        222222222,
                        333333333,
                        444444444,
                        1,
                        2,
                        555555555,
                        666666666
                    };
                    if (o is {
                            Aaaaaaaaaaaaaaaaaaaaaaaaa: 1111111111, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb: 2222222222, Cccccccccccc: 3
                        }) { }
                }
            }
            """
        );

    /// <summary>
    ///     A switch expression, an anonymous object, <c>with</c>, object and array initializers with a blank after
    ///     <c>{</c> and before <c>}</c>.
    /// </summary>
    [Fact]
    public void BracedListsAtTheExport() =>
        Agrees(
            """
            class K {
                void A() {
                    var s = v switch {

                        1 => 2,
                        _ => 3

                    };
                    var an = new {

                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333

                    };
                    var w = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr with {

                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333

                    };
                    var o = new Ooooooooooooooooooooooooooooooooooo {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333

                    };
                    var arr = new int[] {
                        111111111, 222222222, 333333333, 444444444, 111111111, 222222222, 333333333, 444444444, 1, 2,
                        555555555, 666666666

                    };
                }
            }
            """,
            """
            class K {
                void A() {
                    var s = v switch {
                        1 => 2,
                        _ => 3
                    };
                    var an = new {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333
                    };
                    var w = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr with {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333
                    };
                    var o = new Ooooooooooooooooooooooooooooooooooo {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333
                    };
                    var arr = new int[] {
                        111111111, 222222222, 333333333, 444444444, 111111111, 222222222, 333333333, 444444444, 1, 2, 555555555,
                        666666666
                    };
                }
            }
            """
        );

    /// <summary>
    ///     A line comment before an initializer's or a pattern's <c>}</c>, a block comment, and a line comment after an
    ///     initializer's <c>{</c>.
    /// </summary>
    [Fact]
    public void ALineCommentBeforeAnInitializersBrace() =>
        Agrees(
            """
            class K {
                void A() {
                    var an = new {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333 // e

                    };
                    var w = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr with {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333 // e

                    };
                    var o = new Ooooooooooooooooooooooooooooooooooo {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333 /* e */

                    };
                    var arr = new int[] {
                        1, 2 /* e */

                    };
                    var ar2 = new int[] {
                        1, 2 // e

                    };
                    var ar3 = new int[] { // e

                        1, 2
                    };
                    if (o is {
                            Aaaaaaaaaaaaaaaaaaaaaaaaa: 1111111111, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb: 2222222222, Cccccccccccc: 3 // e

                        }) {
                    }
                    var s = v switch {
                        1 => 2, // e

                        _ => 3 // e

                    };
                }
            }
            """,
            """
            class K {
                void A() {
                    var an = new {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333 // e
                    };
                    var w = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr with {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333 // e
                    };
                    var o = new Ooooooooooooooooooooooooooooooooooo {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333 /* e */
                    };
                    var arr = new int[] { 1, 2 /* e */ };
                    var ar2 = new int[] {
                        1, 2 // e
                    };
                    var ar3 = new int[] { // e

                        1, 2
                    };
                    if (o is {
                            Aaaaaaaaaaaaaaaaaaaaaaaaa: 1111111111, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb: 2222222222, Cccccccccccc: 3 // e
                        }) { }

                    var s = v switch {
                        1 => 2, // e

                        _ => 3 // e
                    };
                }
            }
            """
        );

    /// <summary>
    ///     Inside an initializer, an anonymous object and a property pattern a blank after a <c>//</c> comment stays; in
    ///     an attribute list, a type-argument list and a conditional it goes, and after a <c>/* */</c> it goes everywhere.
    /// </summary>
    [Fact]
    public void AfterALineComment_BracesKeepTheBlankAndParenthesesDoNot() =>
        Agrees(
            """
            class K {
                [Aaaaaaaaaaaaaaaaa, // e

                 Bbbbbbbbbbbbbbbbbbbb]
                void A() {
                    var ar = new int[] {
                        1, // e

                        2
                    };
                    var an = new {
                        A = 1, // e

                        B = 2
                    };
                    var ar4 = new int[] {
                        1, /* e */

                        2
                    };
                    if (o is {
                            A: 1, // e

                            B: 2
                        }) {
                    }
                    if (o is {
                            A: 1,
                            // e

                            B: 2
                        }) {
                    }
                    var x = new D { // e

                        A = 1, B = 2
                    };
                    Foo<int, // e

                        string>();
                    var y = a // e

                        ? b
                        : c;
                }
            }
            """,
            """
            class K {
                [Aaaaaaaaaaaaaaaaa, // e
                 Bbbbbbbbbbbbbbbbbbbb]
                void A() {
                    var ar = new int[] {
                        1, // e

                        2
                    };
                    var an = new {
                        A = 1, // e

                        B = 2
                    };
                    var ar4 = new int[] { 1, /* e */ 2 };
                    if (o is {
                            A: 1, // e

                            B: 2
                        }) { }

                    if (o is {
                            A: 1,
                            // e

                            B: 2
                        }) { }

                    var x = new D { // e

                        A = 1, B = 2
                    };
                    Foo<int, // e
                        string>();
                    var y = a // e
                        ? b
                        : c;
                }
            }
            """
        );

    /// <summary>
    ///     The same input with <c>skala_remove_blank_lines_near_braces_in_code = false</c>: an initializer's or a
    ///     pattern's <c>}</c> keeps a blank after a line comment and loses one after an item; a switch expression's and a
    ///     collection expression's closers keep theirs.
    /// </summary>
    [Fact]
    public void NearBracesInCodeOff_C() =>
        Agrees(
            """
            class K {
                void A() {
                    var x = aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa +

                        bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb + cccccccccccccccccccccccccccccccccccccc;
                    var y = source

                        .Select(z => z)

                        .Where(z => z);
                    var s = v switch {
                        1 => 2,

                        _ => 3
                    };
                    var o = new Ooo {
                        A = 1,

                        B = 2
                    };
                    var l = new List<int> {
                        111111111, 222222222, 333333333, 444444444, 111111111, 222222222, 333333333, 444444444, 111111111, 222222222,

                        555555555, 666666666
                    };
                    Foo(x => {
                        M();

                        N();

                    });
                    Foo(x => {
                        M();

                        N();
                    }, 2

                    );
                    var q = from a in b

                        where a

                        select a;
                    var p = cond

                        ? 1
                        : 2;
                    if (a &&

                        b) {
                    }
                    Bar(
                        1,
                        // own line

                        2);
                    Bar(
                        1,

                        // own line
                        2);
                }

                [Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,

                 Bbbbbbbbbbbbbbbbbbbbbbbbb]
                void B<T, U>()
                    where T : class

                    where U : struct {
                }
            }

            class D : Aaaaaaaaaaaaaaaaaaaaa,

                Bbbbbbbbbbbbbbbbbbbbbb {
            }
            """,
            """
            class K {
                void A() {
                    var x = aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                        + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                        + cccccccccccccccccccccccccccccccccccccc;
                    var y = source
                        .Select(z => z)
                        .Where(z => z);
                    var s = v switch {
                        1 => 2,

                        _ => 3
                    };
                    var o = new Ooo { A = 1, B = 2 };
                    var l = new List<int> {
                        111111111,
                        222222222,
                        333333333,
                        444444444,
                        111111111,
                        222222222,
                        333333333,
                        444444444,
                        111111111,
                        222222222,
                        555555555,
                        666666666
                    };
                    Foo(x => {
                            M();

                            N();

                        }
                    );
                    Foo(
                        x => {
                            M();

                            N();
                        },
                        2
                    );
                    var q = from a in b
                        where a
                        select a;
                    var p = cond
                        ? 1
                        : 2;
                    if (a && b) { }

                    Bar(
                        1,
                        // own line
                        2
                    );
                    Bar(
                        1,

                        // own line
                        2
                    );
                }

                [Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                 Bbbbbbbbbbbbbbbbbbbbbbbbb]
                void B<T, U>()
                    where T : class
                    where U : struct { }
            }

            class D : Aaaaaaaaaaaaaaaaaaaaa,
                Bbbbbbbbbbbbbbbbbbbbbb { }
            """,
            ("skala_remove_blank_lines_near_braces_in_code", "false")
        );

    /// <summary>
    ///     The same input with <c>skala_remove_blank_lines_near_braces_in_code = false</c>: an initializer's or a
    ///     pattern's <c>}</c> keeps a blank after a line comment and loses one after an item; a switch expression's and a
    ///     collection expression's closers keep theirs.
    /// </summary>
    [Fact]
    public void NearBracesInCodeOff_E() =>
        Agrees(
            """
            class K {
                int f1 = 1,

                    f2 = 2;

                int P {
                    get => 1;

                    set { }
                }

                void A() {
                    var o = new Ooooooooooooooooooooooooooooooooooo {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,

                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333
                    };
                    var an = new {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,

                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333
                    };
                    int[] ce = [
                        111111111, 222222222, 333333333, 444444444, 111111111, 222222222, 333333333, 444444444,

                        555555555, 666666666, 111111111, 222222222, 333333333
                    ];
                    var w = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr with {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,

                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333
                    };
                    if (o is {

                        Aaaaaaaaaaaaaaaaaaaaaaaaa: 1111111111, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb: 2222222222, Cccccccccccc: 3
                    }) {
                    }
                    int a = 1,

                        b = 2;
                    for (int i = 0;

                        i < 10; i++) {
                    }
                    var s = v switch {
                        1 => 2, // c

                        _ => 3
                    };
                    var s2 = v switch {
                        1 => 2,
                        // c

                        _ => 3
                    };
                    switch (v) {
                        case 1:

                        case 2:
                            break;
                    }
                    Bar(1,
                        // c

                        // d
                        2);
                }

                void Lo(int a,
                    // c

                    int b) {
                }
            }

            enum E {
                A,

                B
            }

            record R(int A,

                int B);
            """,
            """
            class K {
                int f1 = 1,
                    f2 = 2;

                int P {
                    get => 1;

                    set { }
                }

                void A() {
                    var o = new Ooooooooooooooooooooooooooooooooooo {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333
                    };
                    var an = new {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333
                    };
                    int[] ce = [
                        111111111, 222222222, 333333333, 444444444, 111111111, 222222222, 333333333, 444444444,

                        555555555, 666666666, 111111111, 222222222, 333333333
                    ];
                    var w = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr with {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333
                    };
                    if (o is {
                            Aaaaaaaaaaaaaaaaaaaaaaaaa: 1111111111, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb: 2222222222, Cccccccccccc: 3
                        }) { }

                    int a = 1,
                        b = 2;
                    for (int i = 0;
                         i < 10;
                         i++) { }

                    var s = v switch {
                        1 => 2, // c

                        _ => 3
                    };
                    var s2 = v switch {
                        1 => 2,
                        // c

                        _ => 3
                    };
                    switch (v) {
                        case 1:

                        case 2:
                            break;
                    }

                    Bar(
                        1,
                        // c

                        // d
                        2
                    );
                }

                void Lo(
                    int a,
                    // c
                    int b
                ) { }
            }

            enum E {
                A,

                B
            }

            record R(
                int A,
                int B);
            """,
            ("skala_remove_blank_lines_near_braces_in_code", "false")
        );

    /// <summary>
    ///     The same input with <c>skala_remove_blank_lines_near_braces_in_code = false</c>: an initializer's or a
    ///     pattern's <c>}</c> keeps a blank after a line comment and loses one after an item; a switch expression's and a
    ///     collection expression's closers keep theirs.
    /// </summary>
    [Fact]
    public void NearBracesInCodeOff_G() =>
        Agrees(
            """
            class K {
                [A]

                [B]
                void M1() {
                }

                void A() {
                    do {
                        x();
                    }

                    while (a);
                    var s = v switch {
                        1 => 2,
                        _ => 3

                    };
                    int[] ce = [
                        1, 2, 3

                    ];
                    var l = new List<int> {
                        1, 2, 3

                    };
                    if (a) {
                        x();

                    }
                    try {
                        x();
                    }

                    catch {
                    }

                    finally {
                    }
                    Foo(x => {
                        M();

                    });
                }
            }
            """,
            """
            class K {
                [A]
                [B]
                void M1() { }

                void A() {
                    do {
                        x();
                    } while (a);

                    var s = v switch {
                        1 => 2,
                        _ => 3

                    };
                    int[] ce = [
                        1, 2, 3

                    ];
                    var l = new List<int> { 1, 2, 3 };
                    if (a) {
                        x();

                    }

                    try {
                        x();
                    } catch { } finally { }

                    Foo(x => { M(); });
                }
            }
            """,
            ("skala_remove_blank_lines_near_braces_in_code", "false")
        );

    /// <summary>
    ///     The same input with <c>skala_remove_blank_lines_near_braces_in_code = false</c>: an initializer's or a
    ///     pattern's <c>}</c> keeps a blank after a line comment and loses one after an item; a switch expression's and a
    ///     collection expression's closers keep theirs.
    /// </summary>
    [Fact]
    public void NearBracesInCodeOff_I() =>
        Agrees(
            """
            class K {
                void A() {
                    var s = v switch {

                        1 => 2,
                        _ => 3

                    };
                    var an = new {

                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333

                    };
                    var w = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr with {

                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333

                    };
                    var o = new Ooooooooooooooooooooooooooooooooooo {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333

                    };
                    var arr = new int[] {
                        111111111, 222222222, 333333333, 444444444, 111111111, 222222222, 333333333, 444444444, 1, 2,
                        555555555, 666666666

                    };
                }
            }
            """,
            """
            class K {
                void A() {
                    var s = v switch {

                        1 => 2,
                        _ => 3

                    };
                    var an = new {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333
                    };
                    var w = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr with {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333
                    };
                    var o = new Ooooooooooooooooooooooooooooooooooo {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333
                    };
                    var arr = new int[] {
                        111111111, 222222222, 333333333, 444444444, 111111111, 222222222, 333333333, 444444444, 1, 2, 555555555,
                        666666666
                    };
                }
            }
            """,
            ("skala_remove_blank_lines_near_braces_in_code", "false")
        );

    /// <summary>
    ///     The same input with <c>skala_remove_blank_lines_near_braces_in_code = false</c>: an initializer's or a
    ///     pattern's <c>}</c> keeps a blank after a line comment and loses one after an item; a switch expression's and a
    ///     collection expression's closers keep theirs.
    /// </summary>
    [Fact]
    public void NearBracesInCodeOff_J() =>
        Agrees(
            """
            class K {
                void A() {
                    var an = new {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333 // e

                    };
                    var w = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr with {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333 // e

                    };
                    var o = new Ooooooooooooooooooooooooooooooooooo {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333 /* e */

                    };
                    var arr = new int[] {
                        1, 2 /* e */

                    };
                    var ar2 = new int[] {
                        1, 2 // e

                    };
                    var ar3 = new int[] { // e

                        1, 2
                    };
                    if (o is {
                            Aaaaaaaaaaaaaaaaaaaaaaaaa: 1111111111, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb: 2222222222, Cccccccccccc: 3 // e

                        }) {
                    }
                    var s = v switch {
                        1 => 2, // e

                        _ => 3 // e

                    };
                }
            }
            """,
            """
            class K {
                void A() {
                    var an = new {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333 // e

                    };
                    var w = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr with {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333 // e

                    };
                    var o = new Ooooooooooooooooooooooooooooooooooo {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333 /* e */
                    };
                    var arr = new int[] { 1, 2 /* e */ };
                    var ar2 = new int[] {
                        1, 2 // e

                    };
                    var ar3 = new int[] { // e

                        1, 2
                    };
                    if (o is {
                            Aaaaaaaaaaaaaaaaaaaaaaaaa: 1111111111, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb: 2222222222, Cccccccccccc: 3 // e

                        }) { }

                    var s = v switch {
                        1 => 2, // e

                        _ => 3 // e

                    };
                }
            }
            """,
            ("skala_remove_blank_lines_near_braces_in_code", "false")
        );

    /// <summary>
    ///     The same input with <c>skala_remove_blank_lines_near_braces_in_code = false</c>: an initializer's or a
    ///     pattern's <c>}</c> keeps a blank after a line comment and loses one after an item; a switch expression's and a
    ///     collection expression's closers keep theirs.
    /// </summary>
    [Fact]
    public void NearBracesInCodeOff_K() =>
        Agrees(
            """
            class K {
                [Aaaaaaaaaaaaaaaaa, // e

                 Bbbbbbbbbbbbbbbbbbbb]
                void A() {
                    var ar = new int[] {
                        1, // e

                        2
                    };
                    var an = new {
                        A = 1, // e

                        B = 2
                    };
                    var ar4 = new int[] {
                        1, /* e */

                        2
                    };
                    if (o is {
                            A: 1, // e

                            B: 2
                        }) {
                    }
                    if (o is {
                            A: 1,
                            // e

                            B: 2
                        }) {
                    }
                    var x = new D { // e

                        A = 1, B = 2
                    };
                    Foo<int, // e

                        string>();
                    var y = a // e

                        ? b
                        : c;
                }
            }
            """,
            """
            class K {
                [Aaaaaaaaaaaaaaaaa, // e
                 Bbbbbbbbbbbbbbbbbbbb]
                void A() {
                    var ar = new int[] {
                        1, // e

                        2
                    };
                    var an = new {
                        A = 1, // e

                        B = 2
                    };
                    var ar4 = new int[] { 1, /* e */ 2 };
                    if (o is {
                            A: 1, // e

                            B: 2
                        }) { }

                    if (o is {
                            A: 1,
                            // e

                            B: 2
                        }) { }

                    var x = new D { // e

                        A = 1, B = 2
                    };
                    Foo<int, // e
                        string>();
                    var y = a // e
                        ? b
                        : c;
                }
            }
            """,
            ("skala_remove_blank_lines_near_braces_in_code", "false")
        );

    /// <summary>The same input with both keep keys at 0.</summary>
    [Fact]
    public void KeepBlankLinesZero_A() =>
        Agrees(
            """
            class K {
                void A() {
                    M(1, 2

                    );
                    M(1, 2 // e

                    );
                    M(1,
                        2 /*e*/

                    );
                    M(1, 2 /*e*/

                    );
                    var a = new[] { 1, 2 // e

                    };
                    var b = new int[] { 1,
                        2 /*e*/

                    };
                    var x = arr[1, 2 // e

                    ];
                    var y = arr[1,
                        2 /*e*/

                    ];
                    var l = new List<int> { 1, 2 // e

                    };
                    Foo<int, string // e

                    >();
                    Foo<int,
                        string /*e*/

                    >();
                    if (a) {
                        M(); // e

                    }
                    if (a) {
                        M(); /*e*/

                    }
                }

                void B(int a, int b // e

                ) {
                }

                void C(int a,
                    int b /*e*/

                ) {
                }

                [Attr(1, 2 // e

                )]
                void D() {
                }

                int E => F(1, 2 // e

                );
            }
            """,
            """
            class K {
                void A() {
                    M(1, 2);
                    M(
                        1,
                        2 // e
                    );
                    M(
                        1,
                        2 /*e*/
                    );
                    M(
                        1,
                        2 /*e*/
                    );
                    var a = new[] {
                        1, 2 // e
                    };
                    var b = new int[] { 1, 2 /*e*/ };
                    var x = arr[1,
                        2 // e
                    ];
                    var y = arr[1,
                        2 /*e*/
                    ];
                    var l = new List<int> {
                        1, 2 // e
                    };
                    Foo<int, string // e
                    >();
                    Foo<int,
                        string /*e*/
                    >();
                    if (a) {
                        M(); // e
                    }

                    if (a) {
                        M(); /*e*/
                    }
                }

                void B(
                    int a,
                    int b // e
                ) { }

                void C(
                    int a,
                    int b /*e*/
                ) { }

                [Attr(
                    1,
                    2 // e
                )]
                void D() { }

                int E =>
                    F(
                        1,
                        2 // e
                    );
            }
            """,
            ("skala_keep_blank_lines_in_code", "0"),
            ("skala_keep_blank_lines_in_declarations", "0")
        );

    /// <summary>The same input with both keep keys at 0.</summary>
    [Fact]
    public void KeepBlankLinesZero_J() =>
        Agrees(
            """
            class K {
                void A() {
                    var an = new {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333 // e

                    };
                    var w = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr with {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333 // e

                    };
                    var o = new Ooooooooooooooooooooooooooooooooooo {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222, Cccccccccccccccccccccc = 3333333333333 /* e */

                    };
                    var arr = new int[] {
                        1, 2 /* e */

                    };
                    var ar2 = new int[] {
                        1, 2 // e

                    };
                    var ar3 = new int[] { // e

                        1, 2
                    };
                    if (o is {
                            Aaaaaaaaaaaaaaaaaaaaaaaaa: 1111111111, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb: 2222222222, Cccccccccccc: 3 // e

                        }) {
                    }
                    var s = v switch {
                        1 => 2, // e

                        _ => 3 // e

                    };
                }
            }
            """,
            """
            class K {
                void A() {
                    var an = new {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333 // e
                    };
                    var w = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr with {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333 // e
                    };
                    var o = new Ooooooooooooooooooooooooooooooooooo {
                        Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
                        Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
                        Cccccccccccccccccccccc = 3333333333333 /* e */
                    };
                    var arr = new int[] { 1, 2 /* e */ };
                    var ar2 = new int[] {
                        1, 2 // e
                    };
                    var ar3 = new int[] { // e
                        1, 2
                    };
                    if (o is {
                            Aaaaaaaaaaaaaaaaaaaaaaaaa: 1111111111, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb: 2222222222, Cccccccccccc: 3 // e
                        }) { }

                    var s = v switch {
                        1 => 2, // e
                        _ => 3 // e
                    };
                }
            }
            """,
            ("skala_keep_blank_lines_in_code", "0"),
            ("skala_keep_blank_lines_in_declarations", "0")
        );

    /// <summary>The same input with both keep keys at 0.</summary>
    [Fact]
    public void KeepBlankLinesZero_K() =>
        Agrees(
            """
            class K {
                [Aaaaaaaaaaaaaaaaa, // e

                 Bbbbbbbbbbbbbbbbbbbb]
                void A() {
                    var ar = new int[] {
                        1, // e

                        2
                    };
                    var an = new {
                        A = 1, // e

                        B = 2
                    };
                    var ar4 = new int[] {
                        1, /* e */

                        2
                    };
                    if (o is {
                            A: 1, // e

                            B: 2
                        }) {
                    }
                    if (o is {
                            A: 1,
                            // e

                            B: 2
                        }) {
                    }
                    var x = new D { // e

                        A = 1, B = 2
                    };
                    Foo<int, // e

                        string>();
                    var y = a // e

                        ? b
                        : c;
                }
            }
            """,
            """
            class K {
                [Aaaaaaaaaaaaaaaaa, // e
                 Bbbbbbbbbbbbbbbbbbbb]
                void A() {
                    var ar = new int[] {
                        1, // e
                        2
                    };
                    var an = new {
                        A = 1, // e
                        B = 2
                    };
                    var ar4 = new int[] { 1, /* e */ 2 };
                    if (o is {
                            A: 1, // e
                            B: 2
                        }) { }

                    if (o is {
                            A: 1,
                            // e
                            B: 2
                        }) { }

                    var x = new D { // e
                        A = 1, B = 2
                    };
                    Foo<int, // e
                        string>();
                    var y = a // e
                        ? b
                        : c;
                }
            }
            """,
            ("skala_keep_blank_lines_in_code", "0"),
            ("skala_keep_blank_lines_in_declarations", "0")
        );
}
