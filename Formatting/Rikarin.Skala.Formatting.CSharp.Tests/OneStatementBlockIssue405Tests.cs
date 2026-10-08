using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #405, SK-DIV-0162: a one-statement block that may share its owner's line stays there
///     exactly when its statement ends up on that line too. The oracle breaks an accessor's, a lambda's
///     or an anonymous method's block open as soon as what it holds wraps — an argument list the author
///     chopped, a binary the author broke, a switch expression, a sum past the margin, a lambda block of
///     its own — joins one the author broke whose statement fits, and leaves one whose statement
///     re-joins. Every expected string is <c>jb cleanupcode</c>'s own output for the input, and each
///     test asserts the second pass too.
/// </summary>
/// <remarks>
///     The committed fixtures are <c>constructs/breaks/one-statement-block-on-its-owners-line.cs</c> and,
///     under all four preservation corners, <c>constructs/preservation/one-statement-blocks.cs</c>.
/// </remarks>
public sealed class OneStatementBlockIssue405Tests {
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
    ///     <c>get</c>, <c>set</c>, <c>init</c> and <c>add</c>: a chopped call, a broken binary and a switch
    ///     expression each break the block open; <c>Combine(</c>↵<c>value)</c> re-joins, and its block
    ///     stays on the line.
    /// </summary>
    [Fact]
    public void EveryAccessorKind_BreaksOpenWhenItsStatementWraps() =>
        Agrees(
            """
            using System;

            class T {
                int _n;
                event EventHandler _e;

                public int Get {
                    get { return Math.Max(
                        _n,
                        1); }
                }

                public int Set {
                    get { return _n; }
                    set { _n = _n
                        + value; }
                }

                public int Init {
                    get { return _n; }
                    init { _n = value switch { _ => 0 }; }
                }

                public event EventHandler Event {
                    add { _e += Combine(
                        value); }
                    remove { _e -= value; }
                }

                static EventHandler Combine(EventHandler a) => a;
            }
            """,
            """
            using System;

            class T {
                int _n;
                event EventHandler _e;

                public int Get {
                    get {
                        return Math.Max(
                            _n,
                            1
                        );
                    }
                }

                public int Set {
                    get { return _n; }
                    set {
                        _n = _n
                            + value;
                    }
                }

                public int Init {
                    get { return _n; }
                    init {
                        _n = value switch {
                            _ => 0
                        };
                    }
                }

                public event EventHandler Event {
                    add { _e += Combine(value); }
                    remove { _e -= value; }
                }

                static EventHandler Combine(EventHandler a) => a;
            }
            """
        );

    /// <summary>
    ///     A two-statement lambda block goes one statement per line and takes its accessor with it; a
    ///     one-statement one stays. A broken initializer that re-joins leaves its block alone, an
    ///     accessor the author broke is joined, and two statements are never on one line.
    /// </summary>
    [Fact]
    public void ALambdaBlockThatBreaks_BreaksItsAccessor_AndABrokenBlockThatFits_Joins() =>
        Agrees(
            """
            using System;
            using System.Collections.Generic;

            class T {
                int _n;
                Func<int> _f;

                public int OfTwo {
                    get { return _n; }
                    set { _f = () => { _n = 1; return value; }; }
                }

                public int OfOne {
                    get { return _n; }
                    set { _f = () => { return value; }; }
                }

                public List<int> Rejoins {
                    get { return new List<int> {
                        1,
                        2
                    }; }
                }

                public int Broken {
                    get {
                        return _n;
                    }
                    set {
                        _n = value; }
                }

                public int TwoStatements {
                    get { return _n; }
                    set { _n = value; _n++; }
                }
            }
            """,
            """
            using System;
            using System.Collections.Generic;

            class T {
                int _n;
                Func<int> _f;

                public int OfTwo {
                    get { return _n; }
                    set {
                        _f = () => {
                            _n = 1;
                            return value;
                        };
                    }
                }

                public int OfOne {
                    get { return _n; }
                    set { _f = () => { return value; }; }
                }

                public List<int> Rejoins {
                    get { return new List<int> { 1, 2 }; }
                }

                public int Broken {
                    get { return _n; }
                    set { _n = value; }
                }

                public int TwoStatements {
                    get { return _n; }
                    set {
                        _n = value;
                        _n++;
                    }
                }
            }
            """
        );

    /// <summary>
    ///     A 120-column accessor stays; at 121 the block opens and the statement fits on its own line;
    ///     a statement too long for either is chopped once the block is open.
    /// </summary>
    [Fact]
    public void TheMargin_OpensTheBlockFirst() =>
        Agrees(
            """
            using System;

            class T {
                int _n;

                public int AtTheMargin {
                    get { return Math.Max(_n + _n + _n + _n + _n + _n + _n + _n + _n + _n, _n + _n + _n + _n + _n + _n + _n + _n); }
                }

                public int OneColumnPast {
                    get { return Math.Max(_n + _n + _n + _n + _n + _n + _n + _n + _n + _n, _n + _n + _n + _n + _n + _n + _n + 100); }
                }

                public int PastItOpenToo {
                    get { return _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n + _n; }
                }
            }
            """,
            """
            using System;

            class T {
                int _n;

                public int AtTheMargin {
                    get { return Math.Max(_n + _n + _n + _n + _n + _n + _n + _n + _n + _n, _n + _n + _n + _n + _n + _n + _n + _n); }
                }

                public int OneColumnPast {
                    get {
                        return Math.Max(_n + _n + _n + _n + _n + _n + _n + _n + _n + _n, _n + _n + _n + _n + _n + _n + _n + 100);
                    }
                }

                public int PastItOpenToo {
                    get {
                        return _n
                            + _n
                            + _n
                            + _n
                            + _n
                            + _n
                            + _n
                            + _n
                            + _n
                            + _n
                            + _n
                            + _n
                            + _n
                            + _n
                            + _n
                            + _n
                            + _n
                            + _n
                            + _n
                            + _n
                            + _n;
                    }
                }
            }
            """
        );

    /// <summary>
    ///     Lambda and anonymous-method blocks in a method body, as an argument, and two classes deep. A
    ///     single lambda argument keeps its <c>(</c> and the list chops around the block that broke.
    /// </summary>
    [Fact]
    public void LambdaAndAnonymousMethodBlocks_FollowTheSameRule() =>
        Agrees(
            """
            using System;

            class T {
                int _n;
                Action _a;

                void Register(Action a) { }

                void Lambdas() {
                    _a = () => { A(); B(); };
                    _a = () => {
                        A();
                    };
                    _a = () => { _n = Math.Max(
                        _n,
                        1); };
                    Register(() => { A(); });
                    Register(() => { _n = Math.Max(
                        _n,
                        1); });
                    _a = delegate { _n = _n
                        + 1; };
                }

                void A() { }

                void B() { }
            }

            class Outer {
                class Inner {
                    int _n;
                    Action _a;

                    public int P {
                        get { return _n; }
                        set { _a = () => { _n = Math.Max(
                            value,
                            1); }; }
                    }
                }
            }
            """,
            """
            using System;

            class T {
                int _n;
                Action _a;

                void Register(Action a) { }

                void Lambdas() {
                    _a = () => {
                        A();
                        B();
                    };
                    _a = () => { A(); };
                    _a = () => {
                        _n = Math.Max(
                            _n,
                            1
                        );
                    };
                    Register(() => { A(); });
                    Register(() => {
                            _n = Math.Max(
                                _n,
                                1
                            );
                        }
                    );
                    _a = delegate {
                        _n = _n
                            + 1;
                    };
                }

                void A() { }

                void B() { }
            }

            class Outer {
                class Inner {
                    int _n;
                    Action _a;

                    public int P {
                        get { return _n; }
                        set {
                            _a = () => {
                                _n = Math.Max(
                                    value,
                                    1
                                );
                            };
                        }
                    }
                }
            }
            """
        );

    /// <summary>
    ///     <c>skala_keep_existing_declaration_block_arrangement = true</c>: a method's or a local
    ///     function's one-line block stays when its statement does and opens when it wraps; two
    ///     statements still go one per line; the <c>if</c> is the embedded key's and expands; and an
    ///     accessor the author broke stays broken, at both of its gaps.
    /// </summary>
    [Fact]
    public void UnderTheDeclarationKey_AMethodsOneLineBlockFollowsTheSameRule() =>
        Agrees(
            """
            using System;

            class T {
                int _n;

                void OnOneLine() { A(); }

                void Wraps() { _n = Math.Max(
                    _n,
                    1); }

                void TwoStatements() { A(); B(); }

                void Statements() {
                    void Local() { A(); }
                    if (_n > 0) { A(); }
                }

                public int Broken {
                    get {
                        return _n; }
                }

                void A() { }

                void B() { }
            }
            """,
            """
            using System;

            class T {
                int _n;

                void OnOneLine() { A(); }

                void Wraps() {
                    _n = Math.Max(
                        _n,
                        1
                    );
                }

                void TwoStatements() {
                    A();
                    B();
                }

                void Statements() {
                    void Local() { A(); }
                    if (_n > 0) {
                        A();
                    }
                }

                public int Broken {
                    get {
                        return _n;
                    }
                }

                void A() { }

                void B() { }
            }
            """,
            ("skala_keep_existing_declaration_block_arrangement", "true")
        );

    /// <summary>
    ///     <c>skala_keep_existing_embedded_block_arrangement = true</c>: an <c>if</c>'s one-line block
    ///     follows the rule, and a lambda block the author broke stays broken. ⚠ A local function is a
    ///     statement and still answers to the declaration key, so its block expands here.
    /// </summary>
    [Fact]
    public void UnderTheEmbeddedKey_AnIfAndALambdaFollowIt_AndALocalFunctionDoesNot() =>
        Agrees(
            """
            using System;

            class T {
                int _n;
                Action _a;

                void Statements() {
                    if (_n > 0) { A(); }
                    if (_n > 1) { _n = Math.Max(
                        _n,
                        1); }
                    if (_n > 2) { A(); B(); }
                    _a = () => {
                        A(); };
                    _a = () => { A(); };
                }

                void Local() {
                    void L() { A(); }
                }

                void A() { }

                void B() { }
            }
            """,
            """
            using System;

            class T {
                int _n;
                Action _a;

                void Statements() {
                    if (_n > 0) { A(); }

                    if (_n > 1) {
                        _n = Math.Max(
                            _n,
                            1
                        );
                    }

                    if (_n > 2) {
                        A();
                        B();
                    }

                    _a = () => {
                        A();
                    };
                    _a = () => { A(); };
                }

                void Local() {
                    void L() {
                        A();
                    }
                }

                void A() { }

                void B() { }
            }
            """,
            ("skala_keep_existing_embedded_block_arrangement", "true")
        );

    /// <summary>
    ///     SK-DIV-0163: a delegate whose block breaks leaves the call's line, alone or after another
    ///     argument, where a lambda keeps <c>Register(() =&gt; {</c>. And SK-DIV-0077's block half: a
    ///     parameter list the author broke puts the block it heads on lines of its own, for an anonymous
    ///     method and a lambda alike.
    /// </summary>
    [Fact]
    public void AnAnonymousMethodThatBreaks_IsAnOrdinaryArgument_AndABrokenHeadOpensItsBlock() =>
        Agrees(
            """
            using System;

            class T {
                void Register(Action a) { }

                void Register(int x, Action a) { }

                void Use(Func<int, int> f) { }

                void M() {
                    Register(delegate { A(); });
                    Register(delegate { A(); B(); });
                    Register(() => { A(); B(); });
                    Register(1, delegate { A(); B(); });
                    Use(delegate(
                        int first) { return first; });
                    Use((
                        int first) => { return first; });
                }

                void A() { }

                void B() { }
            }
            """,
            """
            using System;

            class T {
                void Register(Action a) { }

                void Register(int x, Action a) { }

                void Use(Func<int, int> f) { }

                void M() {
                    Register(delegate { A(); });
                    Register(
                        delegate {
                            A();
                            B();
                        }
                    );
                    Register(() => {
                            A();
                            B();
                        }
                    );
                    Register(
                        1,
                        delegate {
                            A();
                            B();
                        }
                    );
                    Use(
                        delegate(
                            int first
                        ) {
                            return first;
                        }
                    );
                    Use((
                            int first
                        ) => {
                            return first;
                        }
                    );
                }

                void A() { }

                void B() { }
            }
            """
        );

    /// <summary>
    ///     A method's or a local function's head broken across lines opens the one-line block the key
    ///     would otherwise keep; an accessor's attribute on a line of its own is not its head.
    /// </summary>
    [Fact]
    public void UnderTheDeclarationKey_ABrokenHeadOpensTheBlock_AndAnAttributeLineDoesNot() =>
        Agrees(
            """
            using System;

            class T {
                int _n;

                void N(
                    int x) { A(); }

                void M(bool a, bool b) {
                    void L(
                        int x) { A(); }
                    if (a
                        && b) { A(); }
                }

                public int P {
                    [Obsolete]
                    get { return _n; }
                    [Obsolete] set { _n = value; }
                }

                void A() { }
            }
            """,
            """
            using System;

            class T {
                int _n;

                void N(
                    int x
                ) {
                    A();
                }

                void M(bool a, bool b) {
                    void L(
                        int x
                    ) {
                        A();
                    }

                    if (a
                        && b) {
                        A();
                    }
                }

                public int P {
                    [Obsolete]
                    get { return _n; }
                    [Obsolete]
                    set { _n = value; }
                }

                void A() { }
            }
            """,
            ("skala_keep_existing_declaration_block_arrangement", "true")
        );
}
