using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rikarin.Skala.Rules.Maintainability;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The metrics themselves, pinned to numbers rather than to whether a rule fired.
/// </summary>
/// <remarks>
///     ⚠ A cognitive-complexity implementation that is not pinned to worked examples drifts, and the
///     drift is invisible: the rule keeps firing on the same methods and the number quietly stops being
///     the one SonarQube would print. Every expectation in the § "Sonar's worked examples" region is a
///     number SonarSource published in "Cognitive Complexity — a new way of measuring understandability"
///     (G. Ann Campbell, v1.7), transcribed from Java to C# without changing the control flow. The
///     citation is in the test name; if one of these moves, the metric has stopped being comparable and
///     that is the whole value of it.
/// </remarks>
public sealed class MemberMetricsTests {
    // ---- Sonar's worked examples, with the paper's own totals ----

    /// <summary>Paper § "The implications": <c>getWords</c> scores <b>1</b>.</summary>
    /// <remarks>
    ///     ⚠ The headline result. Cyclomatic complexity scores this 4 and cognitive complexity scores it
    ///     1, "because a switch — which compares a single variable to an explicitly named set of literal
    ///     values — can often be taken in at a glance".
    /// </remarks>
    [Fact]
    public void Paper_GetWords_ASwitchOfFourCases_Scores1() =>
        Assert.Equal(
            1,
            Cognitive(
                """
                class C {
                    string GetWords(int number) {
                        switch (number) {
                            case 1:
                                return "one";
                            case 2:
                                return "a couple";
                            case 3:
                                return "a few";
                            default:
                                return "lots";
                        }
                    }
                }
                """,
                "GetWords"
            )
        );

    /// <summary>Paper § "Increment for nested flow-break structures": <c>myMethod</c> scores <b>9</b>.</summary>
    /// <remarks>
    ///     try is +0 and catch is +1; the if/for/while chain pays 1 + 2 + 3 for its nesting and the if
    ///     inside the catch pays 2 because the catch put it one level down.
    /// </remarks>
    [Fact]
    public void Paper_MyMethod_NestedInsideATryAndACatch_Scores9() =>
        Assert.Equal(
            9,
            Cognitive(
                """
                class C {
                    void MyMethod() {
                        try {
                            if (condition1) {
                                for (int i = 0; i < 10; i++) {
                                    while (condition2) { }
                                }
                            }
                        } catch (Exception1) {
                            if (condition2) { }
                        }
                    }
                }
                """,
                "MyMethod"
            )
        );

    /// <summary>Paper § "Increment for nested flow-break structures": <c>myMethod2</c> scores <b>2</b>.</summary>
    /// <remarks>
    ///     ⚠ "there is no structural increment for lambdas, nested methods, and similar features, such
    ///     methods do increment the nesting level". The lambda costs 0 and makes the <c>if</c> cost 2.
    /// </remarks>
    [Fact]
    public void Paper_MyMethod2_AnIfInsideALambda_Scores2() =>
        Assert.Equal(
            2,
            Cognitive(
                """
                class C {
                    void MyMethod2() {
                        Action r = () => {
                            if (condition1) { }
                        };
                    }
                }
                """,
                "MyMethod2"
            )
        );

    /// <summary>Paper Appendix C, <c>JavaSymbol.overriddenSymbolFrom</c>: <b>19</b>.</summary>
    /// <remarks>
    ///     1 + 1 + (2 + 1) + 3 + 4 + 5 + 1 + 1. The <c>else if</c> at the bottom of the chain is the
    ///     single +1 that proves it takes no nesting increment: at four levels deep, a nesting increment
    ///     would have made it +5.
    /// </remarks>
    [Fact]
    public void Paper_OverriddenSymbolFrom_Scores19() =>
        Assert.Equal(
            19,
            Cognitive(
                """
                class C {
                    object OverriddenSymbolFrom(ClassType classType) {
                        if (classType.IsUnknown) {
                            return Unknown;
                        }

                        bool unknownFound = false;
                        var symbols = classType.Symbol.Members.Lookup(Name);
                        foreach (var overrideSymbol in symbols) {
                            if (overrideSymbol.IsKind(Kind.Method)
                                && !overrideSymbol.IsStatic) {

                                var methodSymbol = (MethodSymbol)overrideSymbol;
                                if (CanOverride(methodSymbol)) {
                                    var overriding = CheckOverridingParameters(methodSymbol, classType);
                                    if (overriding == null) {
                                        if (!unknownFound) {
                                            unknownFound = true;
                                        }
                                    } else if (overriding.Value) {
                                        return methodSymbol;
                                    }
                                }
                            }
                        }

                        if (unknownFound) {
                            return Unknown;
                        }

                        return null;
                    }
                }
                """,
                "OverriddenSymbolFrom"
            )
        );

    /// <summary>Paper Appendix C, <c>WildcardPattern.toRegexp</c>: <b>20</b>.</summary>
    /// <remarks>
    ///     The long <c>else if</c> chain, four of them, each +1, plus two conditions three and four
    ///     levels deep. It is the example that catches an implementation which lets a chain grow.
    /// </remarks>
    [Fact]
    public void Paper_ToRegexp_Scores20() =>
        Assert.Equal(
            20,
            Cognitive(
                """
                class C {
                    string ToRegexp(string antPattern, string directorySeparator) {
                        var escaped = "\\" + directorySeparator;
                        var sb = new StringBuilder(antPattern.Length);
                        sb.Append('^');
                        int i = antPattern.StartsWith("/")
                            || antPattern.StartsWith("\\") ? 1 : 0;

                        while (i < antPattern.Length) {
                            char ch = antPattern[i];
                            if (SpecialChars.IndexOf(ch) != -1) {
                                sb.Append('\\').Append(ch);
                            } else if (ch == '*') {
                                if (i + 1 < antPattern.Length
                                    && antPattern[i + 1] == '*') {

                                    if (i + 2 < antPattern.Length
                                        && IsSlash(antPattern[i + 2])) {
                                        sb.Append("(?:.*");
                                        sb.Append(escaped).Append("|)");
                                        i += 2;
                                    } else {
                                        sb.Append(".*");
                                        i += 1;
                                    }
                                } else {
                                    sb.Append("[^").Append(escaped).Append("]*?");
                                }
                            } else if (ch == '?') {
                                sb.Append("[^").Append(escaped).Append("]");
                            } else if (IsSlash(ch)) {
                                sb.Append(escaped);
                            } else {
                                sb.Append(ch);
                            }

                            i++;
                        }

                        sb.Append('$');
                        return sb.ToString();
                    }
                }
                """,
                "ToRegexp"
            )
        );

    /// <summary>
    ///     Paper § "The implications": <c>sumOfPrimes</c> scores <b>7</b> in Java and <b>10</b> here.
    /// </summary>
    /// <remarks>
    ///     ⚠ The one worked example that does not transcribe. Java's <c>continue OUT;</c> is a jump to a
    ///     label and the paper charges it a flat +1; C# has no labelled <c>continue</c>, so the same
    ///     control flow is written with <c>goto</c> — and SonarSource's own C# analyzer charges
    ///     <c>goto</c> a <em>nesting</em> increment rather than a flat one. At nesting 3 that is +4
    ///     rather than +1, so 7 becomes 10. The divergence is SonarAnalyzer's, not Skala's, and it is
    ///     kept deliberately: a number that does not match what SonarQube prints for this file is a
    ///     number with no reason to exist.
    /// </remarks>
    [Fact]
    public void Paper_SumOfPrimes_WithGotoInPlaceOfALabelledContinue_Scores10() =>
        Assert.Equal(
            10,
            Cognitive(
                """
                class C {
                    int SumOfPrimes(int max) {
                        int total = 0;
                        for (int i = 1; i <= max; ++i) {
                            for (int j = 2; j < i; ++j) {
                                if (i % j == 0) {
                                    goto Next;
                                }
                            }

                            total += i;
                            Next: ;
                        }

                        return total;
                    }
                }
                """,
                "SumOfPrimes"
            )
        );

    // ---- Sequences of binary logical operators ----

    /// <summary>
    ///     Paper § "Sequences of logical operators", every published pair and the mixed examples.
    /// </summary>
    /// <remarks>
    ///     ⚠ Each expectation is the score of the <em>expression</em>: the enclosing <c>if</c>'s own +1
    ///     is subtracted, so these are directly the paper's per-line annotations.
    /// </remarks>
    [Theory]
    [InlineData("a && b", 1)]
    [InlineData("a && b && c", 1)]
    [InlineData("a && b && c && d", 1)]
    [InlineData("a || b", 1)]
    [InlineData("a || b || c || d", 1)]
    [InlineData("a && b || c", 2)]
    [InlineData("a || b && c || d", 2)]
    // The paper's own four-increment example: `if (a && b && c || d || e && f)` is +1 for the `if`
    // and +1 for each of the three sequences.
    [InlineData("a && b && c || d || e && f", 3)]
    // ⚠ `!` interrupts a sequence even though both operators are `&&`. A naive flatten scores 1.
    [InlineData("a && !(b && c)", 2)]
    // Parentheses on their own do not interrupt one.
    [InlineData("(a && b) && c", 1)]
    [InlineData("a && (b && c)", 1)]
    public void ASequenceOfLikeOperators_CostsOne(string condition, int expected) {
        var score = Cognitive(
            "class C { void M() { if (" + condition + ") { } } }",
            "M"
        );

        Assert.Equal(expected + 1, score);
    }

    /// <summary>Pattern combinators read like the operators they replace.</summary>
    [Theory]
    [InlineData("x is int or string or bool", 1)]
    [InlineData("x is > 0 and < 10", 1)]
    [InlineData("x is (> 0 and < 10) or null", 2)]
    public void ASequenceOfLikePatternCombinators_CostsOne(string condition, int expected) {
        var score = Cognitive(
            "class C { void M(object x) { if (" + condition + ") { } } }",
            "M"
        );

        Assert.Equal(expected + 1, score);
    }

    // ---- The two shapes rules.json calls out by name ----

    /// <summary>
    ///     ⚠ A twenty-case <c>switch</c> scores 1 and a triple-nested condition scores 6.
    /// </summary>
    /// <remarks>
    ///     rules.json's <c>SK7002</c> rationale in one assertion: "a switch over twenty cases costs one,
    ///     because a reader takes it in at a glance, and a condition nested three deep costs four rather
    ///     than one". Both sides of that sentence have to be true for the metric to mean anything.
    /// </remarks>
    [Fact]
    public void ATwentyCaseSwitch_ScoresLessThanATripleNestedCondition() {
        var arms = string.Join("\n", Enumerable.Range(1, 20).Select(n => $"case {n}: return {n};"));
        var wide = Cognitive(
            "class C { int Wide(int n) { switch (n) {\n" + arms + "\ndefault: return 0; } } }",
            "Wide"
        );

        // if +1, foreach +2, if +3 — the paper's arithmetic for three levels of nesting.
        var deep = Cognitive(
            """
            class C {
                void Deep() {
                    if (a) {
                        foreach (var x in xs) {
                            if (b) {
                                Use(x);
                            }
                        }
                    }
                }
            }
            """,
            "Deep"
        );

        Assert.Equal(1, wide);
        Assert.Equal(6, deep);
    }

    /// <summary>
    ///     ⚠ An <c>else if</c> chain does not take a nesting increment; a nested <c>if</c> chain does.
    /// </summary>
    /// <remarks>
    ///     The same five conditions written two ways. The chain is 5 — one per branch — and the nest is
    ///     1 + 2 + 3 + 4 + 5 = 15. If an implementation lets <c>else if</c> take a nesting increment the
    ///     two converge, which is the single most common way to get this metric wrong.
    /// </remarks>
    [Fact]
    public void AnElseIfChain_TakesNoNestingIncrement() {
        var chain = Cognitive(
            """
            class C {
                int Chain(int n) {
                    if (n == 1) {
                        return 1;
                    } else if (n == 2) {
                        return 2;
                    } else if (n == 3) {
                        return 3;
                    } else if (n == 4) {
                        return 4;
                    } else {
                        return 5;
                    }
                }
            }
            """,
            "Chain"
        );

        var nest = Cognitive(
            """
            class C {
                int Nest(int n) {
                    if (n > 0) {
                        if (n > 1) {
                            if (n > 2) {
                                if (n > 3) {
                                    if (n > 4) {
                                        return 5;
                                    }
                                }
                            }
                        }
                    }

                    return 0;
                }
            }
            """,
            "Nest"
        );

        Assert.Equal(5, chain);
        Assert.Equal(15, nest);
    }

    /// <summary>
    ///     ⚠ The paper ignores null-coalescing operators by name; <c>try</c> and <c>finally</c> too.
    /// </summary>
    [Theory]
    [InlineData("class C { object M(object a, object b) { return a ?? b; } }", 0)]
    [InlineData("class C { void M(object a) { a ??= new object(); } }", 0)]
    [InlineData("class C { int M(string s) { return s?.Length ?? 0; } }", 0)]
    [InlineData("class C { void M() { try { A(); } finally { B(); } } }", 0)]
    [InlineData("class C { void M() { try { A(); } catch (E) { B(); } finally { C(); } } }", 1)]
    public void ShorthandAndTryFinally_CostNothing(string source, int expected) =>
        Assert.Equal(expected, Cognitive(source, "M"));

    /// <summary>Recursion is +1 once for the method, not once per call site.</summary>
    [Fact]
    public void DirectRecursion_CostsOneForTheMethod() =>
        Assert.Equal(
            2,
            Cognitive(
                """
                class C {
                    int Fib(int n) {
                        if (n < 2) {
                            return n;
                        }

                        return Fib(n - 1) + Fib(n - 2);
                    }
                }
                """,
                "Fib"
            )
        );

    // ---- The other five metrics ----

    /// <summary>Statements, not lines: a <c>for</c> header is one statement, not three.</summary>
    [Fact]
    public void Statements_CountStatementsAndNotBraces() {
        var metrics = Parse(
            """
            class C {
                void M() {
                    int a = 0;
                    for (int i = 0; i < 10; i++) {
                        a += i;
                    }

                    if (a > 0) {
                        a = 0;
                    } else {
                        a = 1;
                    }
                }
            }
            """,
            "M"
        );

        // int a; for; a += i; if; a = 0; a = 1  — six.
        Assert.Equal(6, metrics.Statements);
    }

    /// <summary>⚠ rules.json's SK7006: "a lambda body restarts the count".</summary>
    [Fact]
    public void NestingDepth_RestartsInsideALambda() {
        var direct = Parse(
            """
            class C {
                void M() {
                    if (a) {
                        while (b) {
                            foreach (var x in xs) {
                                if (c) {
                                    Use(x);
                                }
                            }
                        }
                    }
                }
            }
            """,
            "M"
        );

        var throughALambda = Parse(
            """
            class C {
                void M() {
                    if (a) {
                        while (b) {
                            Run(() => {
                                foreach (var x in xs) {
                                    if (c) {
                                        Use(x);
                                    }
                                }
                            });
                        }
                    }
                }
            }
            """,
            "M"
        );

        Assert.Equal(4, direct.NestingDepth);
        Assert.Equal(2, throughALambda.NestingDepth);
    }

    /// <summary>An <c>else if</c> chain is one level of nesting, not five.</summary>
    [Fact]
    public void NestingDepth_DoesNotGrowDownAnElseIfChain() {
        var metrics = Parse(
            """
            class C {
                int M(int n) {
                    if (n == 1) {
                        return 1;
                    } else if (n == 2) {
                        return 2;
                    } else if (n == 3) {
                        return 3;
                    } else {
                        return 4;
                    }
                }
            }
            """,
            "M"
        );

        Assert.Equal(1, metrics.NestingDepth);
    }

    /// <summary>⚠ docs/plan/07 § "Metrics": primary-constructor parameters count.</summary>
    [Fact]
    public void Parameters_IncludeAPrimaryConstructors() {
        var tree = CSharpSyntaxTree.ParseText(
            "class C(int a, int b, int c) { }",
            new CSharpParseOptions(LanguageVersion.Preview),
            cancellationToken: TestContext.Current.CancellationToken
        );

        var type = tree.GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single();

        Assert.Equal(3, MemberMetrics.Compute(type, null, TestContext.Current.CancellationToken).Parameters);
    }

    /// <summary>An extension method's <c>this</c> parameter counts: a caller supplies it.</summary>
    [Fact]
    public void Parameters_IncludeTheExtensionReceiver() =>
        Assert.Equal(
            2,
            Parse("static class C { static void M(this string s, int n) { } }", "M").Parameters
        );

    /// <summary>⚠ rules.json's SK7004: fields are counted separately, and enum members not at all.</summary>
    [Fact]
    public void TypeSize_CountsFieldsSeparatelyAndPerDeclarator() {
        var tree = CSharpSyntaxTree.ParseText(
            """
            class C {
                int a, b, c;
                string d;
                void M1() { }
                void M2() { }
                int P { get; set; }
                class Nested { }
            }
            """,
            new CSharpParseOptions(LanguageVersion.Preview),
            cancellationToken: TestContext.Current.CancellationToken
        );

        var type = tree.GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First();

        var size = MemberMetrics.ComputeTypeSize(type, TestContext.Current.CancellationToken);

        Assert.Equal(4, size.Fields);
        Assert.Equal(4, size.Members);
    }

    // ---- Cyclomatic complexity: the control-flow graph, and the fallback that has to agree ----

    /// <summary>
    ///     ⚠ The control-flow-graph number and the syntactic fallback must agree.
    /// </summary>
    /// <remarks>
    ///     docs/plan/07 § "Metrics" specifies Roslyn's <c>ControlFlowGraph</c>, and docs/plan/07 §
    ///     "loose" gives a whole load mode no semantic model at all. If the two disagree, the same member
    ///     measures differently depending on how the run was loaded — which is the cache's failure mode
    ///     (a number that changes for a reason nothing in the report names) arriving through a different
    ///     door. Every shape here is one the two could plausibly count differently.
    /// </remarks>
    [Theory]
    [InlineData("void M() { }")]
    [InlineData("int M(int a) { return a > 0 ? 1 : 2; }")]
    [InlineData("void M(bool a, bool b) { if (a && b) { A(); } }")]
    [InlineData("void M(bool a, bool b) { if (a || b) { A(); } else { B(); } }")]
    [InlineData("void M(int a) { while (a > 0) { a--; } }")]
    [InlineData("void M(int a) { do { a--; } while (a > 0); }")]
    [InlineData("void M(System.Collections.Generic.List<int> xs) { foreach (var x in xs) { A(); } }")]
    [InlineData("void M(System.Span<int> xs) { foreach (var x in xs) { A(); } }")]
    [InlineData("void M() { for (int i = 0; i < 10; i++) { A(); } }")]
    [InlineData("void M(int a) { for (;;) { a++; if (a > 3) return; } }")]
    [InlineData("void M(object o) { if (o is int or string) { A(); } }")]
    [InlineData("void M() { try { A(); } catch (Exception e) when (e.Message != null) { B(); } }")]
    [InlineData("void M() { try { A(); } catch (Exception) { B(); } finally { A(); } }")]
    [InlineData("void M(Action f) { Action g = () => { if (f != null) { A(); } }; g(); }")]
    [InlineData("void M() { void Local() { if (true) { A(); } } Local(); }")]
    [InlineData("System.Collections.Generic.IEnumerable<int> M() { yield return 1; yield return 2; }")]
    [InlineData("void M(int a) { switch (a) { case 1: A(); break; case 2: B(); break; default: break; } }")]
    [InlineData("int M(int a) { return a switch { 1 => 1, 2 => 2, _ => 0 }; }")]
    [InlineData("int M(string s) { return s?.Length ?? 0; }")]
    [InlineData("void M(object o) { if (o is int i and > 0) { A(); } }")]
    [InlineData("int M(int a, int b) { if (a > 0) { if (b > 0) { return 1; } return 2; } return 3; }")]
    public void CyclomaticComplexity_AgreesBetweenTheGraphAndTheSyntacticFallback(string member) {
        var (withModel, withoutModel) = Both(member);

        Assert.True(withModel.CyclomaticFromControlFlowGraph, "the control-flow graph was not built");
        Assert.False(withoutModel.CyclomaticFromControlFlowGraph);
        Assert.Equal(withoutModel.Cyclomatic, withModel.Cyclomatic);
    }

    /// <summary>
    ///     ⚠ The one shape where the graph and the fallback do not agree, pinned rather than hidden.
    /// </summary>
    /// <remarks>
    ///     <c>foreach</c> over an <c>IEnumerable</c> or an array compiles to a loop plus an implicit
    ///     <c>finally</c> that asks whether the enumerator is disposable, and that question is a
    ///     conditional edge in Roslyn's graph. No amount of looking at the source finds it, because it
    ///     is not in the source. The graph is the definition docs/plan/07 § "Metrics" chose, so the
    ///     graph's number is the right one and the fallback is one short — which is exactly what
    ///     <see cref="MemberMetricValues.CyclomaticFromControlFlowGraph" /> exists to tell a consumer.
    ///     <para>
    ///         It is pinned here so that the day Roslyn changes the lowering, this test fails rather than a
    ///         repository's numbers moving with no explanation.
    ///     </para>
    /// </remarks>
    [Theory]
    [InlineData("void M(int[] xs) { foreach (var x in xs) { A(); } }", 3, 2)]
    [InlineData("void M(System.Collections.Generic.IEnumerable<int> xs) { foreach (var x in xs) { A(); } }", 3, 2)]
    public void CyclomaticComplexity_OfAForeachOverAnIEnumerable_CountsTheImplicitDisposal(
        string member,
        int fromGraph,
        int syntactic
    ) {
        var (withModel, withoutModel) = Both(member);

        Assert.Equal(fromGraph, withModel.Cyclomatic);
        Assert.Equal(syntactic, withoutModel.Cyclomatic);
    }

    /// <summary>
    ///     A property is one member with two bodies, and scores as one member.
    /// </summary>
    [Fact]
    public void CyclomaticComplexity_OfAProperty_CoversBothAccessorsOnce() {
        const string Source = """
                              class Holder {
                                  int backing;

                                  public int Value {
                                      get { return backing > 0 ? backing : 0; }
                                      set { if (value > 0) { backing = value; } }
                                  }
                              }
                              """;

        var compilation = RuleFixtures.Compile(Source, "property.cs");
        var tree = compilation.SyntaxTrees.Single();
        var property = tree.GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .Single();

        var metrics = MemberMetrics.Compute(
            property,
            compilation.GetSemanticModel(tree),
            TestContext.Current.CancellationToken
        );

        Assert.True(metrics.CyclomaticFromControlFlowGraph);

        // One ternary and one `if`: two decisions, so three paths, not four.
        Assert.Equal(3, metrics.Cyclomatic);
    }

    // ---- SK7010's two predicates ----

    [Theory]
    [InlineData("public class C { public void M() { } }", true)]
    [InlineData("internal class C { public void M() { } }", false)]
    [InlineData("public class C { void M() { } }", false)]
    [InlineData("public class C { private void M() { } }", false)]
    [InlineData("public interface C { void M(); }", true)]
    [InlineData("public class Outer { public class C { public void M() { } } }", true)]
    [InlineData("public class Outer { internal class C { public void M() { } } }", false)]
    public void IsPublicApi_FollowsTheWholeContainingChain(string source, bool expected) {
        var tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Preview),
            cancellationToken: TestContext.Current.CancellationToken
        );

        var method = tree.GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single();

        Assert.Equal(expected, MemberMetrics.IsPublicApi(method));
    }

    /// <summary>⚠ rules.json: an <c>&lt;inheritdoc/&gt;</c> is documentation.</summary>
    [Theory]
    [InlineData("/// <summary>A thing.</summary>\npublic void M() { }", true)]
    [InlineData("/// <inheritdoc/>\npublic void M() { }", true)]
    [InlineData("public void M() { }", false)]
    [InlineData("///\npublic void M() { }", false)]
    [InlineData("// an ordinary comment\npublic void M() { }", false)]
    [InlineData("/// <summary>A thing.</summary>\n[Obsolete]\npublic void M() { }", true)]
    public void HasDocumentation_ReadsTheDocCommentAndNotAnyComment(string member, bool expected) {
        var tree = CSharpSyntaxTree.ParseText(
            "public class C {\n" + member + "\n}",
            new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Parse),
            cancellationToken: TestContext.Current.CancellationToken
        );

        var method = tree.GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single();

        Assert.Equal(expected, MemberMetrics.HasDocumentation(method));
    }

    // ---- helpers ----

    static int Cognitive(string source, string memberName) => Parse(source, memberName).Cognitive;

    /// <summary>One member, measured with a semantic model and without one.</summary>
    static (MemberMetricValues WithModel, MemberMetricValues WithoutModel) Both(string member) {
        var source = "using System;\nclass Holder {\n    " + member
            + "\n    void A() { }\n    void B() { }\n}\n";

        var compilation = RuleFixtures.Compile(source, "cyclomatic.cs");
        var tree = compilation.SyntaxTrees.Single();
        var declaration = tree.GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == "M");

        var errors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.True(errors.Length == 0, string.Join("; ", errors.Select(static d => d.ToString())));

        return (
            MemberMetrics.Compute(
                declaration,
                compilation.GetSemanticModel(tree),
                TestContext.Current.CancellationToken
            ),
            MemberMetrics.Compute(declaration, null, TestContext.Current.CancellationToken)
        );
    }

    /// <summary>
    ///     Parse only, no compilation: cognitive complexity is a <c>Syntax</c>-scoped metric and the
    ///     examples above are transcriptions rather than compilable programs.
    /// </summary>
    static MemberMetricValues Parse(string source, string memberName) {
        var tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Preview),
            cancellationToken: TestContext.Current.CancellationToken
        );

        var root = tree.GetRoot(TestContext.Current.CancellationToken);
        var errors = tree.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(
            errors.Length == 0,
            "the example does not parse: " + string.Join("; ", errors.Select(static d => d.ToString()))
        );

        var member = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == memberName);

        return MemberMetrics.Compute(member, null, TestContext.Current.CancellationToken);
    }
}
