// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-17
namespace Constructs.Breaks;

// SK-DIV-0114 (issue #371). A positional pattern's subpatterns are filled exactly as a tuple's
// components are: a kept break after a comma, before a comma or after the `(` comes back as written
// with the next subpattern one level in; a pattern past the margin fills at its commas, in a member
// and in a switch arm alike; and a nested subpattern with a kept break inside keeps its head on the
// outer subpattern's line. Before this file the clause had no plan at all, so a kept break was left
// as written and an overflowing pattern was never wrapped.
public class PositionalPattern {
    bool AfterComma(object o) =>
        o is (1,
            2);

    bool AfterOpen(object o) =>
        o is (
            1, 2);

    bool BeforeComma(object o) =>
        o is (1
            , 2);

    bool TooLong(object o) =>
        o is (SomeVeryLongConstantNumberOne, SomeVeryLongConstantNumberTwo, SomeVeryLongConstantNumberThree,
            SomeVeryLongConstantNumberFour);

    bool Named(object o) =>
        o is (a: 1,
            b: 2);

    bool Nested(object o) =>
        o is (1, (2,
            3));

    int Switch(object o) =>
        o switch {
            (1,
                2) => 1,
            (
                3, 4) => 2,
            (SomeVeryLongConstantNumberOne, SomeVeryLongConstantNumberTwo, SomeVeryLongConstantNumberThree,
                SomeVeryLongConstantNumberFour) => 3,
            _ => 0,
        };

    bool TupleExpressionTwin() =>
        (1,
            2)
        == (1, 2);

    const int SomeVeryLongConstantNumberOne = 0;
    const int SomeVeryLongConstantNumberTwo = 0;
    const int SomeVeryLongConstantNumberThree = 0;
    const int SomeVeryLongConstantNumberFour = 0;
}
