using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #442, SK-DIV-0203: where a kept <c>)</c> on a line of its own lands. A statement header's under its
///     <c>(</c> at <c>align_multiline_statement_conditions = true</c>; a <c>typeof</c>, <c>sizeof</c>,
///     <c>default</c>, <c>checked</c> or <c>unchecked</c> one on its opener's line, as an argument list's; a
///     grouping parenthesis's or a tuple's on a continuation line. Every expected string is
///     <c>jb cleanupcode</c> 2025.2.6's own output for the input, and each test asserts the second pass too.
/// </summary>
public sealed class KeptClosingParenthesisIssue442Tests {
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
    ///     Every statement header, a tuple, <c>typeof</c>, <c>sizeof</c>, <c>default</c>, <c>checked</c>, a grouping
    ///     parenthesis and a cast.
    /// </summary>
    [Fact]
    public void StatementHeadersTuplesAndTypeof_TheExport() =>
        Agrees(
            """
            class K {
                void A() {
                    var t = (1, 2
                    );
                    var t3 = (1, 2 // e
                    );
                    var u = typeof(int
                    );
                    var s = sizeof(int
                    );
                    var d = default(int
                    );
                    var c = checked(a + b
                    );
                    var p = (a + b
                    );
                    var q = (int
                        )x;
                    lock (o
                    ) {
                    }
                    if (a
                    ) {
                    }
                    while (a
                    ) {
                    }
                    using (o
                    ) {
                    }
                    foreach (var x in y
                    ) {
                    }
                    for (int i = 0; i < 1; i++
                    ) {
                    }
                    switch (a
                    ) {
                    }
                    fixed (int* p = a
                    ) {
                    }
                    do {
                    } while (a
                    );
                    if (a &&
                        b
                    ) {
                    }
                }
            }
            """,
            """
            class K {
                void A() {
                    var t = (1, 2
                        );
                    var t3 = (1, 2 // e
                        );
                    var u = typeof(int
                    );
                    var s = sizeof(int
                    );
                    var d = default(int
                    );
                    var c = checked(a + b
                    );
                    var p = (a + b
                        );
                    var q = (int
                        )x;
                    lock (o
                         ) { }

                    if (a
                       ) { }

                    while (a
                          ) { }

                    using (o
                          ) { }

                    foreach (var x in y
                            ) { }

                    for (int i = 0;
                         i < 1;
                         i++
                        ) { }

                    switch (a
                           ) { }

                    fixed (int* p = a
                          ) { }

                    do { } while (a
                                 );

                    if (a && b
                       ) { }
                }
            }
            """
        );

    /// <summary>The same closers inside argument lists, a chain, a broken binary, an expression body and two fields.</summary>
    [Fact]
    public void InArgumentsFieldsAndChains_TheExport() =>
        Agrees(
            """
            class K2 {
                void A() {
                    p = (a + b
                    );
                    M((a + b
                    ), c);
                    M(c, (a + b
                    ));
                    M(c,
                        (a + b
                        ));
                    x.Y((1, 2
                    ));
                    Foo((a
                    ).B);
                    var t = ((1, 2
                    ), 3);
                    M(typeof(int
                    ));
                    M(c,
                        typeof(int
                        ));
                    var z = typeof(int
                    ).Name;
                    var w = 1 + typeof(int
                    ).Name.Length;
                    int F() => (1
                    );
                }

                (int, int) T = (1, 2
                );
                Type U = typeof(int
                );
            }
            """,
            """
            class K2 {
                void A() {
                    p = (a + b
                        );
                    M(
                        (a + b
                        ),
                        c
                    );
                    M(
                        c,
                        (a + b
                        )
                    );
                    M(
                        c,
                        (a + b
                        )
                    );
                    x.Y(
                        (1, 2
                        )
                    );
                    Foo(
                        (a
                        ).B
                    );
                    var t = ((1, 2
                        ), 3);
                    M(
                        typeof(int
                        )
                    );
                    M(
                        c,
                        typeof(int
                        )
                    );
                    var z = typeof(int
                    ).Name;
                    var w = 1
                        + typeof(int
                        ).Name.Length;

                    int F() =>
                        (1
                        );
                }

                (int, int) T = (1, 2
                    );

                Type U = typeof(int
                );
            }
            """
        );

    /// <summary>
    ///     <c>else if</c>, a header holding a chopped call, a grouping parenthesis inside a condition, a <c>catch</c>
    ///     filter, broken contents of <c>typeof</c> and <c>checked</c>, one depth further in.
    /// </summary>
    [Fact]
    public void NestedHeadersAndContents_TheExport() =>
        Agrees(
            """
            namespace N {
                class K3 {
                    void A() {
                        var u = unchecked(a + b
                        );
                        var v = typeof(
                            int);
                        var w = typeof(Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb>);
                        var c = checked(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb * ccccccccccccccccccccccccc);
                        var d = checked(a +
                            b);
                        M(default(int
                        ), x);
                        if (o
                        ) {
                        }
                        else if (p
                        ) {
                        }
                        if (Call(a,
                                b
                            )) {
                        }
                        if (a ||
                            (b
                            )) {
                        }
                        while (Call(
                                   a
                               )
                        ) {
                        }
                        try {
                        } catch (Exception e) when (e
                        ) {
                        }
                        var p = (a +
                            b
                        );
                        var t = (aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccc);
                    }
                }
            }
            """,
            """
            namespace N {
                class K3 {
                    void A() {
                        var u = unchecked(a + b
                        );
                        var v = typeof(
                            int);
                        var w =
                            typeof(Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb>);
                        var c = checked(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                            + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb * ccccccccccccccccccccccccc);
                        var d = checked(a + b);
                        M(
                            default(int
                            ),
                            x
                        );
                        if (o
                           ) { } else if (p
                                         ) { }

                        if (Call(
                                a,
                                b
                            )) { }

                        if (a
                            || (b
                            )) { }

                        while (Call(a)
                              ) { }

                        try { } catch (Exception e) when (e
                                                         ) { }

                        var p = (a + b
                            );
                        var t = (aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
                            cccccccccccccccccccccccccc);
                    }
                }
            }
            """
        );

    /// <summary>At <c>align_multiline_statement_conditions = false</c> a header's <c>)</c> is on the statement's column.</summary>
    [Fact]
    public void StatementHeadersTuplesAndTypeof_ConditionsNotAligned() =>
        Agrees(
            """
            class K {
                void A() {
                    var t = (1, 2
                    );
                    var t3 = (1, 2 // e
                    );
                    var u = typeof(int
                    );
                    var s = sizeof(int
                    );
                    var d = default(int
                    );
                    var c = checked(a + b
                    );
                    var p = (a + b
                    );
                    var q = (int
                        )x;
                    lock (o
                    ) {
                    }
                    if (a
                    ) {
                    }
                    while (a
                    ) {
                    }
                    using (o
                    ) {
                    }
                    foreach (var x in y
                    ) {
                    }
                    for (int i = 0; i < 1; i++
                    ) {
                    }
                    switch (a
                    ) {
                    }
                    fixed (int* p = a
                    ) {
                    }
                    do {
                    } while (a
                    );
                    if (a &&
                        b
                    ) {
                    }
                }
            }
            """,
            """
            class K {
                void A() {
                    var t = (1, 2
                        );
                    var t3 = (1, 2 // e
                        );
                    var u = typeof(int
                    );
                    var s = sizeof(int
                    );
                    var d = default(int
                    );
                    var c = checked(a + b
                    );
                    var p = (a + b
                        );
                    var q = (int
                        )x;
                    lock (o
                    ) { }

                    if (a
                    ) { }

                    while (a
                    ) { }

                    using (o
                    ) { }

                    foreach (var x in y
                    ) { }

                    for (int i = 0;
                        i < 1;
                        i++
                    ) { }

                    switch (a
                    ) { }

                    fixed (int* p = a
                    ) { }

                    do { } while (a
                    );

                    if (a && b
                    ) { }
                }
            }
            """,
            ("skala_align_multiline_statement_conditions", "false")
        );

    /// <summary>The same, inside arguments.</summary>
    [Fact]
    public void InArgumentsFieldsAndChains_ConditionsNotAligned() =>
        Agrees(
            """
            class K2 {
                void A() {
                    p = (a + b
                    );
                    M((a + b
                    ), c);
                    M(c, (a + b
                    ));
                    M(c,
                        (a + b
                        ));
                    x.Y((1, 2
                    ));
                    Foo((a
                    ).B);
                    var t = ((1, 2
                    ), 3);
                    M(typeof(int
                    ));
                    M(c,
                        typeof(int
                        ));
                    var z = typeof(int
                    ).Name;
                    var w = 1 + typeof(int
                    ).Name.Length;
                    int F() => (1
                    );
                }

                (int, int) T = (1, 2
                );
                Type U = typeof(int
                );
            }
            """,
            """
            class K2 {
                void A() {
                    p = (a + b
                        );
                    M(
                        (a + b
                        ),
                        c
                    );
                    M(
                        c,
                        (a + b
                        )
                    );
                    M(
                        c,
                        (a + b
                        )
                    );
                    x.Y(
                        (1, 2
                        )
                    );
                    Foo(
                        (a
                        ).B
                    );
                    var t = ((1, 2
                        ), 3);
                    M(
                        typeof(int
                        )
                    );
                    M(
                        c,
                        typeof(int
                        )
                    );
                    var z = typeof(int
                    ).Name;
                    var w = 1
                        + typeof(int
                        ).Name.Length;

                    int F() =>
                        (1
                        );
                }

                (int, int) T = (1, 2
                    );

                Type U = typeof(int
                );
            }
            """,
            ("skala_align_multiline_statement_conditions", "false")
        );

    /// <summary>The same, nested.</summary>
    [Fact]
    public void NestedHeadersAndContents_ConditionsNotAligned() =>
        Agrees(
            """
            namespace N {
                class K3 {
                    void A() {
                        var u = unchecked(a + b
                        );
                        var v = typeof(
                            int);
                        var w = typeof(Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb>);
                        var c = checked(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb * ccccccccccccccccccccccccc);
                        var d = checked(a +
                            b);
                        M(default(int
                        ), x);
                        if (o
                        ) {
                        }
                        else if (p
                        ) {
                        }
                        if (Call(a,
                                b
                            )) {
                        }
                        if (a ||
                            (b
                            )) {
                        }
                        while (Call(
                                   a
                               )
                        ) {
                        }
                        try {
                        } catch (Exception e) when (e
                        ) {
                        }
                        var p = (a +
                            b
                        );
                        var t = (aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccc);
                    }
                }
            }
            """,
            """
            namespace N {
                class K3 {
                    void A() {
                        var u = unchecked(a + b
                        );
                        var v = typeof(
                            int);
                        var w =
                            typeof(Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb>);
                        var c = checked(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                            + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb * ccccccccccccccccccccccccc);
                        var d = checked(a + b);
                        M(
                            default(int
                            ),
                            x
                        );
                        if (o
                        ) { } else if (p
                        ) { }

                        if (Call(
                                a,
                                b
                            )) { }

                        if (a
                            || (b
                            )) { }

                        while (Call(a)
                        ) { }

                        try { } catch (Exception e) when (e
                        ) { }

                        var p = (a + b
                            );
                        var t = (aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
                            cccccccccccccccccccccccccc);
                    }
                }
            }
            """,
            ("skala_align_multiline_statement_conditions", "false")
        );

    /// <summary>At <c>indent_pars = outside</c> a <c>typeof</c>'s <c>)</c> takes one level, as a bracket's does.</summary>
    [Fact]
    public void StatementHeadersTuplesAndTypeof_IndentParsOutside() =>
        Agrees(
            """
            class K {
                void A() {
                    var t = (1, 2
                    );
                    var t3 = (1, 2 // e
                    );
                    var u = typeof(int
                    );
                    var s = sizeof(int
                    );
                    var d = default(int
                    );
                    var c = checked(a + b
                    );
                    var p = (a + b
                    );
                    var q = (int
                        )x;
                    lock (o
                    ) {
                    }
                    if (a
                    ) {
                    }
                    while (a
                    ) {
                    }
                    using (o
                    ) {
                    }
                    foreach (var x in y
                    ) {
                    }
                    for (int i = 0; i < 1; i++
                    ) {
                    }
                    switch (a
                    ) {
                    }
                    fixed (int* p = a
                    ) {
                    }
                    do {
                    } while (a
                    );
                    if (a &&
                        b
                    ) {
                    }
                }
            }
            """,
            """
            class K {
                void A() {
                    var t = (1, 2
                        );
                    var t3 = (1, 2 // e
                        );
                    var u = typeof(int
                        );
                    var s = sizeof(int
                        );
                    var d = default(int
                        );
                    var c = checked(a + b
                        );
                    var p = (a + b
                        );
                    var q = (int
                        )x;
                    lock (o
                         ) { }

                    if (a
                       ) { }

                    while (a
                          ) { }

                    using (o
                          ) { }

                    foreach (var x in y
                            ) { }

                    for (int i = 0;
                         i < 1;
                         i++
                        ) { }

                    switch (a
                           ) { }

                    fixed (int* p = a
                          ) { }

                    do { } while (a
                                 );

                    if (a && b
                       ) { }
                }
            }
            """,
            ("skala_indent_pars", "outside")
        );
}
