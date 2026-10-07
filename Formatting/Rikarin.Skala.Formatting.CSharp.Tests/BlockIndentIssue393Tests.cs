namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #393, SK-DIV-0148: a brace block nests from where a continuation line of the innermost
///     broken construct around it would start — so through a grouping parenthesis on its own line, and
///     from a binary or chain that breaks after it. Every expected string is <c>jb cleanupcode</c>'s own
///     output for the input; <c>constructs/syntax/block-in-a-grouping-parenthesis.cs</c> and
///     <c>constructs/syntax/block-in-a-broken-construct.cs</c> hold the wider set of shapes.
/// </summary>
public sealed class BlockIndentIssue393Tests {
    [Fact]
    public void AGroupingParenthesis_OnTheBlocksLine_AddsNothing() =>
        Oracle.Agrees(
            """
            class T {
                string Member(int value) => (value switch { 1 => "a", _ => "b" }).ToString();

                int Assigned(int y) {
                    var x = (y switch { 1 => 10, _ => 0 });
                    var z = ((y switch { 1 => 10, _ => 0 }));
                    System.Action a = (() => {
                        M();
                        M();
                    });
                    return x + z;
                }

                void M() { }
            }
            """,
            """
            class T {
                string Member(int value) =>
                    (value switch {
                        1 => "a",
                        _ => "b"
                    }).ToString();

                int Assigned(int y) {
                    var x = (y switch {
                        1 => 10,
                        _ => 0
                    });
                    var z = ((y switch {
                        1 => 10,
                        _ => 0
                    }));
                    System.Action a = (() => {
                        M();
                        M();
                    });
                    return x + z;
                }

                void M() { }
            }
            """
        );

    [Fact]
    public void AnInitializerInAGroupingParenthesis_NestsFromTheStatement() =>
        Oracle.Agrees(
            """
            class T {
                public int Alpha, Bravo, Charlie, Delta, Echo;

                object I(R r) {
                    var x = (new T { Alpha = 1000000, Bravo = 2000000, Charlie = 3000000, Delta = 4000000, Echo = 5000000 });
                    var y = (r with { Alpha = 1000000, Bravo = 2000000, Charlie = 3000000, Delta = 4000000, Echo = 5000 });
                    return N((new T { Alpha = 1000000, Bravo = 2000000, Charlie = 3000000, Delta = 4000000, Echo = 50 }));
                }

                object N(object a) => a;
            }

            record R(int Alpha, int Bravo, int Charlie, int Delta, int Echo);
            """,
            """
            class T {
                public int Alpha, Bravo, Charlie, Delta, Echo;

                object I(R r) {
                    var x = (new T {
                        Alpha = 1000000,
                        Bravo = 2000000,
                        Charlie = 3000000,
                        Delta = 4000000,
                        Echo = 5000000
                    });
                    var y = (r with {
                        Alpha = 1000000,
                        Bravo = 2000000,
                        Charlie = 3000000,
                        Delta = 4000000,
                        Echo = 5000
                    });
                    return N(
                        (new T {
                            Alpha = 1000000,
                            Bravo = 2000000,
                            Charlie = 3000000,
                            Delta = 4000000,
                            Echo = 50
                        })
                    );
                }

                object N(object a) => a;
            }

            record R(int Alpha, int Bravo, int Charlie, int Delta, int Echo);
            """
        );

    [Fact]
    public void ABinaryThatBreaksAfterTheBlock_NestsItFromItsContinuationLine() =>
        Oracle.Agrees(
            """
            class T {
                object A(int y) {
                    var x = y switch {
                        1 => 10,
                        _ => 0
                    } + 1;
                    var z = (y switch {
                        1 => 10,
                        _ => 0
                    } + 1);
                    return x + z;
                }
            }
            """,
            """
            class T {
                object A(int y) {
                    var x = y switch {
                            1 => 10,
                            _ => 0
                        }
                        + 1;
                    var z = (y switch {
                            1 => 10,
                            _ => 0
                        }
                        + 1);
                    return x + z;
                }
            }
            """
        );

    [Fact]
    public void WhereTheContinuationLineSpendsNothing_NeitherDoesTheBlock() =>
        Oracle.Agrees(
            """
            class T {
                int Arrow(int y) =>
                    y switch {
                        1 => 10,
                        _ => 0
                    }
                    + 1;

                object Branch(bool c, int y) {
                    var x = c
                        ? y switch {
                            1 => 10,
                            _ => 0
                        }
                        + 1
                        : 0;
                    return x;
                }
            }
            """,
            """
            class T {
                int Arrow(int y) =>
                    y switch {
                        1 => 10,
                        _ => 0
                    }
                    + 1;

                object Branch(bool c, int y) {
                    var x = c
                        ? y switch {
                            1 => 10,
                            _ => 0
                        }
                        + 1
                        : 0;
                    return x;
                }
            }
            """
        );

    [Fact]
    public void ABlockOnALineTheConstructAlreadyBrokeOnto_IsLeftAlone() =>
        Oracle.Agrees(
            """
            class T {
                object Ternary(bool hasConstants) {
                    var extra = hasConstants
                        ? new[] {
                            F(1)
                            + F(200000000000000000)
                            + F(300000000000000000)
                            + F(400000000000000000)
                            + F(500000000000000000)
                            + F(600000000000000000)
                        }
                        : null;
                    return extra;
                }

                object Chain(System.Type type) {
                    var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(ci => {
                                var parameters = ci.GetParameters();
                                return parameters.Length == 0 || parameters.All(pi => pi.HasDefaultValue);
                            }
                        );
                    return ctor;
                }

                int F(long a) => 0;
            }
            """,
            """
            class T {
                object Ternary(bool hasConstants) {
                    var extra = hasConstants
                        ? new[] {
                            F(1)
                            + F(200000000000000000)
                            + F(300000000000000000)
                            + F(400000000000000000)
                            + F(500000000000000000)
                            + F(600000000000000000)
                        }
                        : null;
                    return extra;
                }

                object Chain(System.Type type) {
                    var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(ci => {
                                var parameters = ci.GetParameters();
                                return parameters.Length == 0 || parameters.All(pi => pi.HasDefaultValue);
                            }
                        );
                    return ctor;
                }

                int F(long a) => 0;
            }
            """
        );

    /// <remarks>
    ///     ⚠ The block's lines only. The oracle puts the <c>?</c> one level past the statement and Skala
    ///     two, which is SK-DIV-0150's and not this rule's; the assertion stops at the <c>}</c>. Skala's
    ///     own <c>ArgumentStyleRule.cs</c> holds the property-pattern shape and drifted under a version
    ///     that let a ternary lift the block.
    /// </remarks>
    [Theory]
    [InlineData(
        """
        class T {
            object A(int y, object v) {
                return y switch {
                    1 => 10,
                    _ => 0
                } is 1
                    ? v
                    : null;
            }
        }
        """
    )]
    [InlineData(
        """
        class T {
            object A(int y, object v) {
                return (y switch {
                    1 => 10,
                    _ => 0
                })
                    ? v
                    : null;
            }
        }
        """
    )]
    [InlineData(
        """
        class T {
            object A(object node, object v) {
                return node is string {
                    Length: 1000000000, Alpha: IdentifierNameSyntax { Identifier.ValueText: "var" }, Bravo: 2000000
                }
                    ? v
                    : null;
            }
        }
        """
    )]
    public void ATernaryThatBreaksAfterTheBlock_DoesNotLiftIt(string source) {
        var once = Format.Text(source);
        var lines = once.Split('\n');
        var question = Array.FindIndex(lines, static line => line.TrimStart().StartsWith('?'));
        Assert.True(question > 0, once);
        var expected = source.Split('\n')[..question];
        Assert.Equal(string.Join('\n', expected), string.Join('\n', lines[..question]));
        Assert.Equal(once, Format.Text(once));
    }
}
