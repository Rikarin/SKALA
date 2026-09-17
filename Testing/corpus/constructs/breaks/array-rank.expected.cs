// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-17
namespace Constructs.Breaks;

// SK-DIV-0114 (issue #371). An array creation's rank specifier is filled exactly as a tuple's
// components are: a kept break after a comma, before a comma, after the `[` or before the `]` comes
// back as written, with the next size one level in and the `]` on the owner's indent, and a rank
// past the margin fills at its commas. A rank whose sizes are all omitted (`int[,]`) is left to
// keep_user_linebreaks. Before this file the rank had no plan at all, so an overflowing one was
// broken at an operator inside a size rather than at a comma.
public class ArrayRank {
    int[,] AfterComma() =>
        new int[1,
            2];

    int[,] AfterOpen() =>
        new int[
            1, 2];

    int[,] BeforeComma() =>
        new int[1
            , 2];

    int[,] BeforeClose() =>
        new int[1, 2
        ];

    int[,] TooLong() =>
        new int[SomeVeryLongExpressionNumberOne + SomeVeryLongExpressionNumberTwo,
            SomeVeryLongExpressionNumberThree + SomeVeryLongExpressionNumberFour];

    int[,
    ] OmittedSizes;

    int[
        ,] OmittedSizesAfterOpen;

    void Local() {
        var x = new int[1,
            2];
        int[,] y = new int[
            1, 2];
    }

    const int SomeVeryLongExpressionNumberOne = 0;
    const int SomeVeryLongExpressionNumberTwo = 0;
    const int SomeVeryLongExpressionNumberThree = 0;
    const int SomeVeryLongExpressionNumberFour = 0;
}
