using System;

namespace Constructs.Breaks;

// Issue #375 (the Nightly's seed 5209185227727739433). A break the author wrote after an `=` whose
// value is a collection expression is kept exactly when the collection fits flat on the line below,
// 120 columns included and 121 not, and otherwise the bracket takes the break: `x = [` with the
// elements one level in and `]` on the owner's indent, whether the bracket breaks because it is too
// wide, because the author broke it at one of its own gaps, or because an element inside it spans
// lines. The same holds for a deconstruction, a deconstruction assignment, a field, a property, a
// parameter default, an initializer element, a named attribute argument and a `for` header, and it
// is the bracket's rule alone: `=` followed by `new[] {` or a tuple keeps the break around the same
// multi-line element. Before this file the break was kept from the source and joined only when the
// bracket was already broken there, so a list chopped around a multi-line element kept the `=`
// break on pass one and joined it on pass two.
public class CollectionAfterEq {
    static readonly object[] Field =
        [1, () => {
            A();
            B();
        }];

    static object[] Property { get; } =
        [1, 2];

    static void Locals() {
        int[] fits =
            [1, 2];
        object[] element =
            [1, () => {
                A();
                B();
            }];
        int[] gap =
            [
                1, 2
            ];
        int[] wide =
            [1000000, 2000000, 3000000, 4000000, 5000000, 6000000, 7000000, 8000000, 9000000, 10000000, 11000000, 12000000, 13];
        int[] exactlyOneHundredAndTwenty =
            [1000000, 2000000, 3000000, 4000000, 5000000, 6000000, 7000000, 8000000, 9000000, 10000000, 11000000, 1234];
        int[] oneHundredAndTwentyOne =
            [1000000, 2000000, 3000000, 4000000, 5000000, 6000000, 7000000, 8000000, 9000000, 10000000, 11000000, 12345];
        int[] comment =
            [1, // c
                2];
    }

    static void Deconstruction() {
        var (a, b) =
            [1, 2];
        var (c, d) =
            [1, () => {
                A();
                B();
            }];
        object e, f;
        (e, f) =
            [1, 2];
        (e, f) =
            [1, () => {
                A();
                B();
            }];
    }

    static void Parameter(object[] a =
        [1, 2], object[] b =
        [1, () => {
            A();
            B();
        }]) { }

    static void Initializer() {
        var o = new Holder { X =
            [1], Y = 2 };
        var p = new Holder { X =
            [1, () => {
                A();
                B();
            }], Y = 2 };
    }

    [Wide(Values =
        [1, 2])]
    [Wide(Values =
        [1000000, 2000000, 3000000, 4000000, 5000000, 6000000, 7000000, 8000000, 9000000, 10000000, 11000000, 12000000, 13])]
    static void Attributed() { }

    static void Header() {
        for (object[] i =
            [1, () => {
                A();
                B();
            }]; i != null; i = null) { }
    }

    static void NotTheBracket() {
        var t =
            new object[] { 1, () => {
                A();
                B();
            } };
        var u =
            (1, () => {
                A();
                B();
            });
    }

    static void A() { }

    static void B() { }

    class Holder {
        public object[] X;
        public int Y;
    }

    [AttributeUsage(AttributeTargets.All)]
    class WideAttribute : Attribute {
        public int[] Values;
    }
}
