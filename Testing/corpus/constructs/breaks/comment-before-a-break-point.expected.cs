// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
namespace Constructs.Breaks;

// SK-DIV-0165 (issue #409). A break point whose gap holds a block comment breaks after the comment,
// never before it: the comment stays on the line of the token in front of it. That holds for every
// list the export re-lays, for a closer, an opener, an operator, a `.`, a `?`, an `=` and a lambda's
// arrow, and for a comment the author put on a line of its own. The two exceptions are a comment
// right after `(` and after an expression body's `=>`, where the wrap stops at the comment. Before
// this file no construct had a comment inside a wrapped list, and Skala lost the point entirely.
public class CommentBeforeABreakPoint : IAlphaInterfaceNameValue, /* f */
    IBetaInterfaceNameValue,
    IGammaInterfaceNameValu {
    void NamedArguments() {
        var a = new D(
            name175: nameof(value), /* f */
            name176: Cast<ValueTask<Dictionary<string, Guid?>>, int>($"n={items[0]} and {source?.Value}", 12345)
        );
        var b = new D(
            name175: nameof(value), /* f */ /* g */
            name176: Cast<ValueTask<Dictionary<string, Guid?>>, int>($"n={items[0]} and {source?.Value}", 1)
        );
        var c = new D(
            name175: nameof(value), /* f */
            name176: Cast<ValueTask<Dictionary<string, Guid?>>, int>($"n={items[0]} and {source?.Value}", 12345)
        );
        var d = new D(
            name175: nameof(value),
            /* f */
            name176: Cast<ValueTask<Dictionary<string, Guid?>>, int>($"n={items[0]} and {source?.Value}", 12345)
        );
    }

    void Arguments() {
        M(
            alpha,
            /* f */
            beta
        );
        M(
            alpha, /* f */
            beta
        );
        Compute(
            alphaArgumentValue, /* f */
            betaArgumentValue,
            gammaArgumentValue, /* g */
            deltaArgumentValue,
            epsilonArgumentVa
        );
        Compute(
            alphaArgumentValue,
            betaArgumentValue,
            gammaArgumentValue,
            deltaArgumentValue,
            epsilonArgumentValue /* f */
        );
    }

    void Parameters(
        int alphaParameterValue, /* f */
        string betaParameterValue,
        long gammaParameterValue,
        double deltaParame
    ) { }

    [Attr(
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", /* f */
        Name = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
    )]
    void TypeArgumentsAndDeclarators() {
        Dictionary<VeryLongTypeNameNumberOne, /* f */
            VeryLongTypeNameNumberTwo<VeryLongTypeNameNumberThree, int>> field = null;
        int alphaArgumentValue = 1, /* f */
            betaArgumentValue = 2,
            gammaArgumentValue = 3,
            deltaArgumentValue = 444444444;
    }

    enum E {
        A, /* f */
        B,
        C
    }

    void ArmsOperatorsAndDots() {
        var s = x switch {
            1 => "a", /* f */
            2 => "b",
            _ => "c"
        };
        var x = alphaArgumentValue
            + betaArgumentValue /* f */
            + gammaArgumentValue
            + deltaArgumentValue
            + epsilonArgumentVa;
        var y = source.Where(x => x.Alpha)
            .Select(y => y.Beta) /* f */
            .OrderBy(z => z.Gamma)
            .ToList()
            .ToArray()
            .Reverse();
        var z = alphaArgumentValueLongName /* f */
            ? betaArgumentValueLongName
            : gammaArgumentValueLongName + deltaArgumentV;
    }

    void Collections() {
        var xs = new[] { /* f */
            "aaaaaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbbbbbbb", "cccccccccccccccccccccc", "ddddddddddddd"
        };
        int[] ys = [ /* f */
            alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentVa
        ];
        int[] zs = [
            alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgum /* f */
        ];
    }

    void AfterAnEqualsOrALambdasArrow() {
        var alphaArgumentValueLongName = /* f */
            "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss";
        Func<int, string> f = x => /* f */
            "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss";
    }

    void AfterAParenthesis() {
        var a = new D( /* f */ name175: nameof(value),
            name176: Cast<ValueTask<Dictionary<string, Guid?>>, int>($"n={items[0]} and {source?.Value}", 12345)
        );
        Compute( /* f */ alphaArgumentValue,
            betaArgumentValue,
            gammaArgumentValue,
            deltaArgumentValue,
            epsilonArgumentVa
        );
    }

    int AfterAnExpressionBodysArrow => /* f */ Compute(
        alphaArgumentValue,
        betaArgumentValue,
        gammaArgumentValue,
        deltaArg
    );

    void AnAuthorsBreakAroundTheComment() {
        var t = (alpha, /* f */
            beta);
        var u = (alpha,
            /* f */ beta);
        G<int,
            /* f */ string> g = null;
        var (x,
            /* f */ y) = t;
        var xs = new[] {
            1,
            /* f */ 2
        };
        var z = a
            /* f */
            + b;
        if (o is [
                1,
                /* f */ 2
            ]) { }
    }

    void ListsThatFitOrFill() {
        var a = new D(name175: nameof(value), /* f */ name176: 1);
        Compute(alphaArgumentValue, /* f */ betaArgumentValue);
        var t = (alphaArgumentValue, /* f */ betaArgumentValue, gammaArgumentValue, deltaArgumentValue,
            epsilonArgumentValu1);
        var xs = new List<string> {
            "aaaaaaaaaaaaaaaaaaaa", /* f */ "bbbbbbbbbbbbbbbbbbbbbbbb", "cccccccccccccccccccccc", "ddddddddddd"
        };
        var k = new K {
            Alpha = alphaArgumentValue, /* f */ Beta = betaArgumentValue, Gamma = gammaArgumentValue, Delta = 1
        };
    }
}
