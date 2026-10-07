namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #418, SK-DIV-0184: a delimited list opened on the first line of a chained call or a binary
///     operator that broke after it nests from that construct's continuation line, and its closer sits
///     on it — SK-DIV-0148's block rule, applied to a list (SK-DIV-0149). Every expected string is
///     <c>jb cleanupcode</c>'s own output for the input, and each is checked on a second pass;
///     <c>constructs/breaks/chain-first-call-arguments.cs</c> holds the wider set.
/// </summary>
public sealed class ChainFirstCallArgumentsIssue418Tests {
    /// <summary>
    ///     The issue's own input: arguments two levels past the statement and the <c>)</c> on the chain's continuation
    ///     line, with the dots.
    /// </summary>
    [Fact]
    public void TheIssuesInput_NestsTheArgumentsFromTheChainsContinuationLine() =>
        Oracle.Agrees(
            """
            class K {
                void M() {
                    Outer(first: 1, source.Select(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa).Where(beta));
                }

                void N() {
                    var x = source.Select(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa).Where(beta);
                }
            }
            """,
            """
            class K {
                void M() {
                    Outer(
                        first: 1,
                        source.Select(
                                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                            )
                            .Where(beta)
                    );
                }

                void N() {
                    var x = source.Select(
                            aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                        )
                        .Where(beta);
                }
            }
            """
        );

    /// <summary>
    ///     A name, a member access, <c>this</c>, a <c>new</c>, a dot-less head, a <c>?.</c>, a cast, an <c>await</c>, an
    ///     indexer or a property after the call: all lift. A parenthesised head under the <c>=</c> lifts too, because the
    ///     chain spends its own level there.
    /// </summary>
    [Fact]
    public void EveryChainRoot_LiftsTheFirstCallsArguments() =>
        Oracle.Agrees(
            """
            class K {
                async void M() {
                    var a = source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta);
                    var b = this.source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta);
                    var c = this.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta);
                    var d = new Foo().Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta);
                    var e = Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta);
                    var f = source?.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other)?.Where(beta);
                    var g = (object)source.Select<int>(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta);
                    var h = await source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta);
                    var j = source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other)[0].Where(beta);
                    var k = source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Count.ToString();
                    var l = (left ?? right).Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta);
                }
            }
            """,
            """
            class K {
                async void M() {
                    var a = source.Select(
                            selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                            other
                        )
                        .Where(beta);
                    var b = this.source.Select(
                            selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                            other
                        )
                        .Where(beta);
                    var c = this.Select(
                            selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                            other
                        )
                        .Where(beta);
                    var d = new Foo().Select(
                            selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                            other
                        )
                        .Where(beta);
                    var e = Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other)
                        .Where(beta);
                    var f = source?.Select(
                            selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                            other
                        )
                        ?.Where(beta);
                    var g = (object)source.Select<int>(
                            selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                            other
                        )
                        .Where(beta);
                    var h = await source.Select(
                            selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                            other
                        )
                        .Where(beta);
                    var j = source.Select(
                            selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                            other
                        )[0]
                        .Where(beta);
                    var k = source.Select(
                            selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                            other
                        )
                        .Count.ToString();
                    var l = (left ?? right).Select(
                            selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                            other
                        )
                        .Where(beta);
                }
            }
            """
        );

    /// <summary>
    ///     An expression body, a <c>return</c>, a statement, an assignment and an argument; under an argument list the
    ///     chain's level is spent on the item's line and the list nests one past it, and a parenthesised head there spends
    ///     nothing, so there is nothing to lift.
    /// </summary>
    [Fact]
    public void EveryOwner_LiftsFromWhereTheChainContinues() =>
        Oracle.Agrees(
            """
            class K {
                object Body() => source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta).ToList();

                object Returned() {
                    return source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta).ToList();
                }

                void Statements() {
                    source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta).ToList();
                    this.field = source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta);
                    if (c) {
                        Outer(first: 1, source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta));
                    }

                    Outer(first: 1, (left ?? right).Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta));
                }
            }
            """,
            """
            class K {
                object Body() =>
                    source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other)
                        .Where(beta)
                        .ToList();

                object Returned() {
                    return source.Select(
                            selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                            other
                        )
                        .Where(beta)
                        .ToList();
                }

                void Statements() {
                    source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other)
                        .Where(beta)
                        .ToList();
                    this.field = source.Select(
                            selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                            other
                        )
                        .Where(beta);
                    if (c) {
                        Outer(
                            first: 1,
                            source.Select(
                                    selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                                    other
                                )
                                .Where(beta)
                        );
                    }

                    Outer(
                        first: 1,
                        (left ?? right).Select(
                            selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                            other
                        )
                        .Where(beta)
                    );
                }
            }
            """
        );

    /// <summary>
    ///     The first operand of a broken <c>??</c>, <c>+</c> and <c>&amp;&amp;</c> (SK-DIV-0149's two rows); inside an
    ///     argument list the operator spends nothing and the list keeps the ordinary level.
    /// </summary>
    [Fact]
    public void ABinaryOperatorsFirstOperand_LiftsAsAChainDoes() =>
        Oracle.Agrees(
            """
            class K {
                void M() {
                    var x = source.F(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other) ?? fallback;
                    var y = F(
                        first,
                        second
                    )
                        + 1;
                    var ok = items.Any(x => {
                        First();
                        return x;
                    }
                    )
                        && flag;
                    Outer(first: 1, source.F(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other) ?? fallback);
                }
            }
            """,
            """
            class K {
                void M() {
                    var x = source.F(
                            selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                            other
                        )
                        ?? fallback;
                    var y = F(
                            first,
                            second
                        )
                        + 1;
                    var ok = items.Any(x => {
                                First();
                                return x;
                            }
                        )
                        && flag;
                    Outer(
                        first: 1,
                        source.F(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other)
                        ?? fallback
                    );
                }
            }
            """
        );

    /// <summary>
    ///     A call inside the lifted list and a lambda's block — alone, or beside another argument — nest from the lifted
    ///     list.
    /// </summary>
    [Fact]
    public void AListInsideTheLiftedList_AndABlock_NestFromIt() =>
        Oracle.Agrees(
            """
            class K {
                void M() {
                    var x = source.Select(Inner(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other)).Where(beta);
                    var y = source.Select(x => {
                        First();
                        Second(x);
                    }).Where(beta);
                    var z = source.Select(x => {
                        First();
                        Second(x);
                    }, selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe).Where(beta);
                }
            }
            """,
            """
            class K {
                void M() {
                    var x = source.Select(
                            Inner(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other)
                        )
                        .Where(beta);
                    var y = source.Select(x => {
                                First();
                                Second(x);
                            }
                        )
                        .Where(beta);
                    var z = source.Select(
                            x => {
                                First();
                                Second(x);
                            },
                            selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe
                        )
                        .Where(beta);
                }
            }
            """
        );

    /// <summary>
    ///     ⚠ Skala's own source, as master formatted it: a chain or a pattern that opens and breaks inside a
    ///     single-lambda list on the list's own line keeps its continuation where it was, while the list's <c>)</c> moves to
    ///     the outer chain's line — and a call opened on that line still lifts.
    /// </summary>
    [Fact]
    public void ABrokenConstructOpenedOnTheListsLine_ContinuesTheOrdinaryWay() =>
        Oracle.Agrees(
            """
            class K {
                static void AssertTheFixCompiles(string source, string id) {
                    var edits = found.SelectMany(static diagnostic => Enumerable.Range(0, EditCount(diagnostic))
                            .Select(index => (
                                    Start: Number(diagnostic, FixEdits.StartKey(index)),
                                    Length: Number(diagnostic, FixEdits.LengthKey(index)),
                                    Text: diagnostic.Properties[FixEdits.TextKey(index)] ?? string.Empty
                                )
                            )
                    )
                        .OrderByDescending(static edit => edit.Start)
                        .ToArray();
                }

                static IEnumerable<LiteralExpressionSyntax> Literals(SyntaxNode scope) =>
                    scope.DescendantNodes(static node => node is not (AnonymousFunctionExpressionSyntax
                            or LocalFunctionStatementSyntax
                            or InterpolatedStringExpressionSyntax)
                    )
                        .OfType<LiteralExpressionSyntax>()
                        .Where(static node => node.IsKind(SyntaxKind.StringLiteralExpression));

                object Nested() {
                    var x = source.Select(a => Foo(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasured, b)).Where(beta);
                    return x;
                }
            }
            """,
            """
            class K {
                static void AssertTheFixCompiles(string source, string id) {
                    var edits = found.SelectMany(static diagnostic => Enumerable.Range(0, EditCount(diagnostic))
                            .Select(index => (
                                    Start: Number(diagnostic, FixEdits.StartKey(index)),
                                    Length: Number(diagnostic, FixEdits.LengthKey(index)),
                                    Text: diagnostic.Properties[FixEdits.TextKey(index)] ?? string.Empty
                                )
                            )
                        )
                        .OrderByDescending(static edit => edit.Start)
                        .ToArray();
                }

                static IEnumerable<LiteralExpressionSyntax> Literals(SyntaxNode scope) =>
                    scope.DescendantNodes(static node => node is not (AnonymousFunctionExpressionSyntax
                            or LocalFunctionStatementSyntax
                            or InterpolatedStringExpressionSyntax)
                        )
                        .OfType<LiteralExpressionSyntax>()
                        .Where(static node => node.IsKind(SyntaxKind.StringLiteralExpression));

                object Nested() {
                    var x = source.Select(a => Foo(
                                selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasured,
                                b
                            )
                        )
                        .Where(beta);
                    return x;
                }
            }
            """
        );

    /// <summary><c>continuous_indent_multiplier = 2</c>: the list lifts by the chain's eight columns and nests eight more.</summary>
    [Fact]
    public void AtTwiceTheContinuationIndent_TheListLiftsByTheConstructsLevel() =>
        Assert.Equal(
            """
            class K {
                void M() {
                    Outer(
                            first: 1,
                            source.Select(
                                            aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                    )
                                    .Where(beta)
                    );
                }

                void N() {
                    var x = source.Select(
                                    aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                            )
                            .Where(beta);
                }
            }
            """,
            Overridden.Settled(
                    """
                    class K {
                        void M() {
                            Outer(first: 1, source.Select(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa).Where(beta));
                        }

                        void N() {
                            var x = source.Select(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa).Where(beta);
                        }
                    }
                    """,
                    [("skala_continuous_indent_multiplier", "2")]
                )
                .TrimEnd('\n')
        );

    /// <summary><c>continuous_indent_multiplier = 2</c> over every owner.</summary>
    [Fact]
    public void AtTwiceTheContinuationIndent_EveryOwnerLifts() =>
        Assert.Equal(
            """
            class K {
                object Body() =>
                        source.Select(
                                        selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                                        other
                                )
                                .Where(beta)
                                .ToList();

                object Returned() {
                    return source.Select(
                                    selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                                    other
                            )
                            .Where(beta)
                            .ToList();
                }

                void Statements() {
                    source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other)
                            .Where(beta)
                            .ToList();
                    this.field = source.Select(
                                    selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                                    other
                            )
                            .Where(beta);
                    if (c) {
                        Outer(
                                first: 1,
                                source.Select(
                                                selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                                                other
                                        )
                                        .Where(beta)
                        );
                    }

                    Outer(
                            first: 1,
                            (left ?? right).Select(
                                    selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                                    other
                            )
                            .Where(beta)
                    );
                }
            }
            """,
            Overridden.Settled(
                    """
                    class K {
                        object Body() => source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta).ToList();

                        object Returned() {
                            return source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta).ToList();
                        }

                        void Statements() {
                            source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta).ToList();
                            this.field = source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta);
                            if (c) {
                                Outer(first: 1, source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta));
                            }

                            Outer(first: 1, (left ?? right).Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta));
                        }
                    }
                    """,
                    [("skala_continuous_indent_multiplier", "2")]
                )
                .TrimEnd('\n')
        );

    /// <summary>
    ///     ⚠ <c>wrap_if_long</c>: the chain and the operator stay whole after the list or the block, and the oracle lifts
    ///     neither. Skala lifts nothing under a fill (SK-DIV-0185); before #418 it lifted every block.
    /// </summary>
    [Fact]
    public void AFill_KeepsTheOrdinaryLevel() =>
        Assert.Equal(
            """
            class K {
                void M() {
                    var x = source.Select(
                        selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                        other
                    ).Where(beta);
                    var y = source.Select(x => {
                            First();
                            Second(x);
                        }
                    ).Where(beta);
                    var z = source.F(x => {
                            First();
                            Second(x);
                        }
                    ) ?? fallback;
                    Outer(
                        first: 1,
                        source.Select(
                            selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe,
                            other
                        ).Where(beta)
                    );
                }
            }
            """,
            Overridden.Settled(
                    """
                    class K {
                        void M() {
                            var x = source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta);
                            var y = source.Select(x => {
                                First();
                                Second(x);
                            }).Where(beta);
                            var z = source.F(x => {
                                First();
                                Second(x);
                            }) ?? fallback;
                            Outer(first: 1, source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta));
                        }
                    }
                    """,
                    [
                        ("skala_wrap_chained_method_calls", "wrap_if_long"),
                        ("skala_wrap_chained_binary_expressions", "wrap_if_long")
                    ]
                )
                .TrimEnd('\n')
        );
}
