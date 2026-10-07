namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #413, SK-DIV-0164: an anonymous function's block nests from the line the function starts
///     on, not from the line its <c>{</c> lands on — the switch expression's anchor (SK-DIV-0107). Every
///     expected string is <c>jb cleanupcode</c>'s own output, and each input is what Skala wrote before
///     the fix, which the oracle maps back to the expected string;
///     <c>constructs/syntax/block-after-a-broken-head.cs</c> holds the wider set.
/// </summary>
public sealed class AnonymousFunctionBlockIndentIssue413Tests {
    [Fact]
    public void AnAnonymousMethod_UnderAnAssignment_NestsFromTheStatement() =>
        Oracle.Agrees(
            """
            using System;

            class C {
                Func<int, int> _f;

                void M() {
                    _f = delegate(
                        int first
                    ) {
                            return first;
                        };
                }

                class D {
                    Func<int, int> _f;

                    void M() {
                        _f = delegate(
                            int first
                        ) {
                                return first;
                            };
                    }
                }
            }
            """,
            """
            using System;

            class C {
                Func<int, int> _f;

                void M() {
                    _f = delegate(
                        int first
                    ) {
                        return first;
                    };
                }

                class D {
                    Func<int, int> _f;

                    void M() {
                        _f = delegate(
                            int first
                        ) {
                            return first;
                        };
                    }
                }
            }
            """
        );

    [Fact]
    public void ALambda_WithItsParametersOrItsArrowBroken_NestsFromTheStatement() =>
        Oracle.Agrees(
            """
            using System;

            class C {
                Func<int, int, int> _g;
                Func<int, int> _f;

                void M() {
                    _g = (
                        int first,
                        int second
                    ) => {
                            return first;
                        };
                    _f = (
                        int first
                    ) => {
                            return first;
                        };
                    _f = (int first)
                        => {
                            return first;
                        };
                    _f = first
                        => {
                            return first;
                        };
                }

                class D {
                    Func<int, int> _f;

                    void M() {
                        _f = (int first)
                            => {
                                return first;
                            };
                    }
                }
            }
            """,
            """
            using System;

            class C {
                Func<int, int, int> _g;
                Func<int, int> _f;

                void M() {
                    _g = (
                        int first,
                        int second
                    ) => {
                        return first;
                    };
                    _f = (
                        int first
                    ) => {
                        return first;
                    };
                    _f = (int first)
                        => {
                        return first;
                    };
                    _f = first
                        => {
                        return first;
                    };
                }

                class D {
                    Func<int, int> _f;

                    void M() {
                        _f = (int first)
                            => {
                            return first;
                        };
                    }
                }
            }
            """
        );

    [Fact]
    public void ADeclarationAFieldAndACompoundAssignment_NestFromTheirOwnLine() =>
        Oracle.Agrees(
            """
            using System;
            using System.Threading.Tasks;

            class C {
                Func<int, int> _f = delegate(
                    int first
                ) {
                        return first;
                    };

                void M() {
                    Func<int, int> f = delegate(
                        int first
                    ) {
                            return first;
                        };
                    var g = (
                        int first,
                        int second
                    ) => {
                            return first;
                        };
                    _f += delegate(
                        int first
                    ) {
                            return first;
                        };
                    Func<int, Task<int>> a = async (
                        int first
                    ) => {
                            return await Task.FromResult(first);
                        };
                    _f = static (
                        int first
                    ) => {
                            return first;
                        };
                }
            }
            """,
            """
            using System;
            using System.Threading.Tasks;

            class C {
                Func<int, int> _f = delegate(
                    int first
                ) {
                    return first;
                };

                void M() {
                    Func<int, int> f = delegate(
                        int first
                    ) {
                        return first;
                    };
                    var g = (
                        int first,
                        int second
                    ) => {
                        return first;
                    };
                    _f += delegate(
                        int first
                    ) {
                        return first;
                    };
                    Func<int, Task<int>> a = async (
                        int first
                    ) => {
                        return await Task.FromResult(first);
                    };
                    _f = static (
                        int first
                    ) => {
                        return first;
                    };
                }
            }
            """
        );

    /// <summary>
    ///     The controls, which agreed before the fix: a function that starts on a line of its own nests
    ///     from that line, whatever continuation it is on — an argument, an <c>=</c> the author broke,
    ///     a <c>??</c>, a ternary branch, an expression body's arrow — and one that starts on a line
    ///     with no continuation open nests from it, as after <c>return</c>.
    /// </summary>
    [Fact]
    public void AFunctionStartingOnAContinuationLine_NestsFromThatLine() =>
        Oracle.Agrees(
            """
            using System;

            class C {
                Func<int, int> _f;

                Func<int, int> P =>
                    delegate(
                        int first
                    ) {
                        return first;
                    };

                Func<int, int, int> N() {
                    return (
                        int first,
                        int second
                    ) => {
                        return first;
                    };
                }

                void M(bool flag) {
                    _f =
                        delegate(
                            int first
                        ) {
                            return first;
                        };
                    _f = _f
                        ?? delegate(
                            int first
                        ) {
                            return first;
                        };
                    _f = flag
                        ? delegate(
                            int first
                        ) {
                            return first;
                        }
                        : null;
                    Use(
                        1,
                        (
                            int first,
                            int second
                        ) => {
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

                static void Use(Func<int, int> f) { }
                static void Use(int a, Func<int, int, int> f) { }
            }
            """,
            """
            using System;

            class C {
                Func<int, int> _f;

                Func<int, int> P =>
                    delegate(
                        int first
                    ) {
                        return first;
                    };

                Func<int, int, int> N() {
                    return (
                        int first,
                        int second
                    ) => {
                        return first;
                    };
                }

                void M(bool flag) {
                    _f =
                        delegate(
                            int first
                        ) {
                            return first;
                        };
                    _f = _f
                        ?? delegate(
                            int first
                        ) {
                            return first;
                        };
                    _f = flag
                        ? delegate(
                            int first
                        ) {
                            return first;
                        }
                        : null;
                    Use(
                        1,
                        (
                            int first,
                            int second
                        ) => {
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

                static void Use(Func<int, int> f) { }
                static void Use(int a, Func<int, int, int> f) { }
            }
            """
        );

    /// <summary>
    ///     The declarations the issue asked about beside the functions, which agreed before the fix: a
    ///     member's block is absolute, and a constructor's base call and a local function's parameter
    ///     list do not move it.
    /// </summary>
    [Fact]
    public void AMethodAConstructorAndALocalFunction_AreUnmoved() =>
        Oracle.Agrees(
            """
            class B {
                public B(int a, int b) { }
            }

            class C : B {
                public C(int x)
                    : base(
                        x,
                        2
                    ) {
                    Use(x);
                }

                void N(
                    int x
                ) {
                    Use(x);
                }

                void M() {
                    int L(
                        int x
                    ) {
                        return x;
                    }

                    L(1);
                }

                static void Use(int x) { }
            }
            """,
            """
            class B {
                public B(int a, int b) { }
            }

            class C : B {
                public C(int x)
                    : base(
                        x,
                        2
                    ) {
                    Use(x);
                }

                void N(
                    int x
                ) {
                    Use(x);
                }

                void M() {
                    int L(
                        int x
                    ) {
                        return x;
                    }

                    L(1);
                }

                static void Use(int x) { }
            }
            """
        );

    /// <summary>
    ///     ⚠ Not only a function: an object creation's initializer after an argument list the
    ///     fitter chopped nests from the line the creation starts on, explicit or target-typed, an
    ///     object or a collection initializer, under an <c>=</c>, a declaration, a field, a
    ///     <c>return</c>, and from its own line under a <c>??</c> or as an argument.
    /// </summary>
    [Fact]
    public void AnObjectCreationsInitializer_NestsFromTheCreationsLine() =>
        Oracle.Agrees(
            """
            using System;
            using System.Collections.Generic;

            class T {
                public T(int a, int b, int c) { }
                public int P { get; set; }
                public int Q { get; set; }
            }

            class C {
                T _t;
                List<int> _l;

                T _field = new T(
                    aVeryLongArgumentNameNumberOne,
                    aVeryLongArgumentNameNumberTwo,
                    aVeryLongArgumentNameNumberThree
                ) { P = aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo, Q = aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo };

                const int aVeryLongArgumentNameNumberOne = 1, aVeryLongArgumentNameNumberTwo = 2, aVeryLongArgumentNameNumberThree = 3;

                T M() {
                    T i = new(
                        aVeryLongArgumentNameNumberOne,
                        aVeryLongArgumentNameNumberTwo,
                        aVeryLongArgumentNameNumberThree
                    ) { P = aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo, Q = aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo };
                    _l = new List<int>(
                        aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo + aVeryLongArgumentNameNumberThree
                    ) { aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo, aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo };
                    Use(new T(
                        aVeryLongArgumentNameNumberOne,
                        aVeryLongArgumentNameNumberTwo,
                        aVeryLongArgumentNameNumberThree
                    ) { P = aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo, Q = aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo });
                    _t = _t ?? new T(
                        aVeryLongArgumentNameNumberOne,
                        aVeryLongArgumentNameNumberTwo,
                        aVeryLongArgumentNameNumberThree
                    ) { P = aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo, Q = aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo };
                    return new T(
                        aVeryLongArgumentNameNumberOne,
                        aVeryLongArgumentNameNumberTwo,
                        aVeryLongArgumentNameNumberThree
                    ) { P = aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo, Q = aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo };
                }

                static void Use(T t) { }

                class D {
                    T _t;

                    void M() {
                        _t = new T(
                            aVeryLongArgumentNameNumberOne,
                            aVeryLongArgumentNameNumberTwo,
                            aVeryLongArgumentNameNumberThree
                        ) { P = aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo, Q = aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo };
                    }
                }
            }
            """,
            """
            using System;
            using System.Collections.Generic;

            class T {
                public T(int a, int b, int c) { }
                public int P { get; set; }
                public int Q { get; set; }
            }

            class C {
                T _t;
                List<int> _l;

                T _field = new T(
                    aVeryLongArgumentNameNumberOne,
                    aVeryLongArgumentNameNumberTwo,
                    aVeryLongArgumentNameNumberThree
                ) {
                    P = aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo,
                    Q = aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo
                };

                const int aVeryLongArgumentNameNumberOne = 1,
                    aVeryLongArgumentNameNumberTwo = 2,
                    aVeryLongArgumentNameNumberThree = 3;

                T M() {
                    T i = new(
                        aVeryLongArgumentNameNumberOne,
                        aVeryLongArgumentNameNumberTwo,
                        aVeryLongArgumentNameNumberThree
                    ) {
                        P = aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo,
                        Q = aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo
                    };
                    _l = new List<int>(
                        aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo + aVeryLongArgumentNameNumberThree
                    ) {
                        aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo,
                        aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo
                    };
                    Use(
                        new T(
                            aVeryLongArgumentNameNumberOne,
                            aVeryLongArgumentNameNumberTwo,
                            aVeryLongArgumentNameNumberThree
                        ) {
                            P = aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo,
                            Q = aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo
                        }
                    );
                    _t = _t
                        ?? new T(
                            aVeryLongArgumentNameNumberOne,
                            aVeryLongArgumentNameNumberTwo,
                            aVeryLongArgumentNameNumberThree
                        ) {
                            P = aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo,
                            Q = aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo
                        };
                    return new T(
                        aVeryLongArgumentNameNumberOne,
                        aVeryLongArgumentNameNumberTwo,
                        aVeryLongArgumentNameNumberThree
                    ) {
                        P = aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo,
                        Q = aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo
                    };
                }

                static void Use(T t) { }

                class D {
                    T _t;

                    void M() {
                        _t = new T(
                            aVeryLongArgumentNameNumberOne,
                            aVeryLongArgumentNameNumberTwo,
                            aVeryLongArgumentNameNumberThree
                        ) {
                            P = aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo,
                            Q = aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo
                        };
                    }
                }
            }
            """
        );
}
