// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
namespace Constructs.Breaks;

// Issue #495 (SK-DIV-0184). A chain that is the whole condition of an `if`, `else if`, `while` or `do`
// puts its dots on the aligned column, and one that is the body of a call's sole lambda argument one
// level past the statement: the parenthesis already paid. A `!` or an `&&` in the condition, a
// `switch`, `foreach` or `using` header, and a lambda after another argument keep the chain's own level.
public class ChainInAHeaderOrASoleLambda {
    void N() {
        if (source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThreeeeee)
            .Any(predicateValue)) {
            A();
        } else if (source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberTh)
                   .Any(predicateValue)) {
            A();
        }

        while (source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThreeeee)
               .Any(predicateValue)) {
            A();
        }

        do {
            A();
        } while (source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberTh)
                 .Any(predicateValue));

        switch (source.Select(
                        alphaArgumentValueNumberOne,
                        betaArgumentValueNumberTwo,
                        gammaArgumentValueNumberThreeeeee
                    )
                    .Any(predicateValue)) {
            default: break;
        }

        foreach (var item in source.Select(
                         alphaArgumentValueNumberOne,
                         betaArgumentValueNumberTwo,
                         gammaArgumentValueNum
                     )
                     .Where(p)) {
            A();
        }

        using (source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThreeee)
                   .Any(p)) {
            A();
        }

        if (!source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThreeeeee)
                .Any(predicateValue)) {
            A();
        }

        if (flag
            && source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumber)
                .Any(predicateValue)) {
            A();
        }

        if (source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo)
            .Where(gammaArgumentValueNumberThreeeeee)
            .Any(p)) {
            A();
        }

        Use(x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaaaaaaaaaa)
            .Where(predicateValue)
        );
        Use(x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo)
            .Where(predicateValueeeeeeeeeeeeeeeee)
            .ToList()
        );
        Use(
            first,
            x => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaaa)
                .Where(predicateValue)
        );
        Use((x, y) => source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaaaaa)
            .Where(predicateValue)
        );
        Use(x => !source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaaaaaaaaa)
            .Any(predicateValue)
        );
    }
}
