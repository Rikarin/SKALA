// A recursive pattern whose type is followed by a positional clause — `o is Point(2, 3)` — occurred
// nowhere in the corpus, and that absence is how #373 survived: `SpaceRules.BeforeOpenParen` named
// `is Point (1, 2)` as its own example of "an identifier before a parenthesis that is not a call",
// and wrote the space into every typed positional pattern from the first spacing commit on. Asked,
// the oracle governs neither way: `Point(2, 3)` stays closed, `Point (2, 3)` stays spaced and
// `Point  (2, 3)` collapses to one space — the same ungoverned class as the range operator's gap —
// and none of the seven parenthesis and angle keys flipped against it moves it. So every typed shape
// here is written twice, closed and spaced, and the fixture answers "is the author's gap kept" in
// both directions. The untyped clauses and the `var` deconstruction at the end are the control: those
// gaps are governed, and the oracle inserts the space into `is(1, 2)` and `var(a, b)`.
record Point(int X, int Y);

class PositionalPatternAfterItsType {
    bool Typed(object o) => o is Point(2, 3);

    bool TypedSpaced(object o) => o is Point (2, 3);

    bool TypedRun(object o) => o is Point  (2, 3);

    bool Nested(object o) => o is (1, Point(2, 3));

    bool NestedSpaced(object o) => o is (1, Point (2, 3));

    bool WithProperties(object o) => o is Point(2, 3) { X: 1 };

    bool WithPropertiesSpaced(object o) => o is Point (2, 3) { X: 1 };

    bool Designated(object o) => o is Point(var x, var y) p && x == y;

    bool Negated(object o) => o is not Point(2, 3);

    bool NegatedSpaced(object o) => o is not Point (2, 3);

    bool Qualified(object o) => o is System.Collections.Generic.KeyValuePair<int, int>(1, 2);

    bool QualifiedSpaced(object o) => o is System.Collections.Generic.KeyValuePair<int, int> (1, 2);

    bool Arm(object o) =>
        o switch {
            Point(2, 3) => true,
            Point (4, 5) => true,
            _ => false
        };

    void Label(object o) {
        switch (o) {
            case Point(2, 3):
                break;
            case Point (4, 5):
                break;
        }
    }

    bool Untyped(object o) => o is(1, 2);

    bool UntypedNegated(object o) => o is not(1, 2);

    bool VarPattern(object o) => o is var(x, y) && x == y;

    void Deconstruction() {
        var(a, b) = (1, 2);
    }
}
