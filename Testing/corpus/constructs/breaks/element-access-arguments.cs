namespace Constructs.Breaks;

// SK-DIV-0114 (issue #371). An element access's bracketed argument list — `grid[i, j]`, and the
// `[key] = value` of an implicit element access in an initializer — is laid out with the invocation
// keys: a kept break after a comma or after the `[` chops the list, a break before a comma is
// joined, a list that overflows the margin chops one argument per line, and a multi-line argument
// chops the list around it. But the brackets are not the parentheses: the oracle never adds a break
// at either bracket, keeps `grid[` and `]` where the author broke, and keeps `]` glued to a chopped
// argument's `)`. Before this file the list had no plan at all, so every one of these came back as
// written.
public class ElementAccessArguments {
    int[,] grid = new int[2, 2];
    int[,,] cube = new int[2, 2, 2];

    int AfterComma() => grid[0,
        1];

    int AfterOpen() => grid[
        0, 1];

    int BeforeComma() => grid[0
        , 1];

    int BeforeClose() => grid[0, 1
        ];

    int TooLong() => grid[SomeVeryLongExpressionNumberOne + SomeVeryLongExpressionNumberTwo, SomeVeryLongExpressionNumberThree + SomeVeryLongExpressionNumberFour];

    int TooLongThree() => cube[SomeVeryLongExpressionNumberOne, SomeVeryLongExpressionNumberTwo, SomeVeryLongExpressionNumberThree, SomeVeryLongExpressionNumberFour];

    int MultilineArgument() => grid[0, Get(1,
        2)];

    int Get(int a, int b) => a;

    int InvocationTwin() => Get(0,
        1);

    void ImplicitElementAccess() {
        var map = new System.Collections.Generic.Dictionary<(int, int), int> {
            [1,
                2] = 3,
            [
                4, 5] = 6,
            [7
                , 8] = 9,
        };
    }

    void Local() {
        var x = grid[0,
            1];
        grid[0,
            1] = 2;
    }

    const int SomeVeryLongExpressionNumberOne = 0;
    const int SomeVeryLongExpressionNumberTwo = 0;
    const int SomeVeryLongExpressionNumberThree = 0;
    const int SomeVeryLongExpressionNumberFour = 0;
}
