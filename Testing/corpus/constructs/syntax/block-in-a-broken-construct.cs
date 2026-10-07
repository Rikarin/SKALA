// A brace block on the first line of a construct that broke after it nests from where that
// construct's continuation line starts: `var x = y switch { … }` / `+ 1;` puts the arms two levels in
// and the `+ 1` at one, and so does `(y switch { … }` / `+ 1)`, where the grouping parenthesis pays
// for the level instead of the operator. Skala nested the first from the statement's level and got the
// second right only because it counted every grouping parenthesis (issue #393, SK-DIV-0148). The
// level is the construct's own continuation line's, so where that line spends nothing neither does the
// block — under an arrow that already broke, in a ternary's branch, inside an argument list broken
// before the argument — and a delimiter opened between the construct and the block still adds its own:
// `if (items.Any(x => {` … `})` / `&& flag)` puts the body three levels past the `if`. The `var ok =`
// form of that shape is not here: its `)` takes the chain's level in the oracle and the argument
// list's in Skala, the list half of the same rule, recorded as SK-DIV-0149. The ternary branch and
// the chained call at the end are the two shapes from `corpus/real/` whose block sits on a line the
// construct already broke onto, which the rule must leave alone. A ternary is not such a construct — a
// switch or a property pattern in its condition keeps its level when the `?` breaks after it, which
// Skala's own `ArgumentStyleRule.cs` found by drifting under the first version — but its `?` line is
// SK-DIV-0150's, so that shape is pinned by `BlockIndentIssue393Tests` instead. The companion file is
// `block-in-a-grouping-parenthesis.cs`.
namespace P;

public class C {
    public object Operand(int y) {
        var x = y switch {
            1 => 10,
            _ => 0
        } + 1;
        return x;
    }

    public object ParenthesisedOperand(int y) {
        var x = (y switch {
            1 => 10,
            _ => 0
        } + 1);
        return x;
    }

    public object SecondOperandBroken(int y) {
        var x = (1 + y switch {
            1 => 10,
            _ => 0
        });
        return x;
    }

    public object MiddleOperand(int y) {
        var x = (y + 1 switch {
            1 => 10,
            _ => 0
        } + y);
        return x;
    }

    public object Initializer() {
        var x = (new T { Alpha = 1000000000, Bravo = 2000000000, Charlie = 30000000, Delta = 400000000000000, Echo = 5000000000000000000 } == null);
        return x;
    }

    public object TwoParenthesised(int y) {
        return (y switch {
            1 => 10,
            _ => 0
        }) + (y switch {
            1 => 10,
            _ => 0
        });
    }

    public object TernaryCondition(int y) {
        var b = y switch {
            1 => 10,
            _ => 0
        } == 1
            ? 1
            : 2;
        return b;
    }

    public void Assignment(int y) {
        z = (y switch {
            1 => 10,
            _ => 0
        })
            + 1;
    }

    public object LambdaAfterTheOperator(T items, bool flag) {
        var ok = flag
            && items.Any(x => {
                M();
                return true;
            });
        return ok;
    }

    public void Condition(int y, bool flag) {
        if (y switch {
            1 => 10,
            _ => 0
        } == 1
            && flag) {
            M();
        }
    }

    public void GroupedCondition(int y) {
        if ((y switch {
            1 => 10,
            _ => 0
        }) == 1) {
            M();
        }
    }

    public void LambdaInACondition(bool flag, T items) {
        if (items.Any(x => {
                M();
                return true;
            })
            && flag) {
            M();
        }
    }

    public object Returned(int y) {
        return y switch {
            1 => 10,
            _ => 0
        }
            + 1;
    }

    public object Lambda() {
        System.Func<int, int> f = y => y switch {
            1 => 10,
            _ => 0
        }
            + 1;
        return f;
    }

    public object LongChain(int y) {
        var x = (y switch {
            1 => 10,
            _ => 0
        }).ToString().Length.ToString().Length.ToString().Length.ToString().Length.ToString().Length.ToString();
        return x;
    }

    public void ArgumentBrokenBefore(int y) {
        M(
            y switch {
                1 => 10,
                _ => 0
            }
            + 1);
    }

    public object ParenthesisBrokenBefore(int y) {
        var x = (
            y switch {
                1 => 10,
                _ => 0
            }
            + 1);
        return x;
    }

    public object TernaryBranch(bool c, int y) {
        var x = c
            ? y switch {
                1 => 10,
                _ => 0
            }
            + 1
            : 0;
        return x;
    }

    public int AfterTheArrow(int y) =>
        y switch {
            1 => 10,
            _ => 0
        }
        + 1;

    public object Argument(int y) {
        var x = F(y switch {
            1 => 10,
            _ => 0
        }
            + 1);
        return x;
    }

    public object BlockOnTheBranchLine(bool hasConstants) {
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

    public object BlockOnTheChainLine(System.Type type) {
        var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(ci => {
                    var parameters = ci.GetParameters();
                    return parameters.Length == 0 || parameters.All(pi => pi.HasDefaultValue);
                }
            );
        return ctor;
    }

    int z;
    public void M() { }
    public void M(int a) { }
    public int F(long a) => 0;
}

public class T {
    public long Alpha;
    public long Bravo;
    public long Charlie;
    public long Delta;
    public long Echo;
    public bool Any(System.Func<int, bool> f) => true;
}

public class Outer {
    public class Middle {
        public class Deep {
            public object Operand(int y) {
                var x = y switch {
                    1 => 10,
                    _ => 0
                } + 1;
                return x;
            }

            public object ParenthesisedOperand(int y) {
                var x = (y switch {
                    1 => 10,
                    _ => 0
                } + 1);
                return x;
            }

            public void M() { }
        }
    }
}
