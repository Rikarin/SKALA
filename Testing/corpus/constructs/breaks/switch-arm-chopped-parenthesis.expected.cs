// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
namespace Constructs.Breaks;

// SK-DIV-0157 (issue #406). A switch arm's body opening with a parenthesis the author broke after is
// SK-DIV-0101's shape with a third owner: the `(` at the arm's indent and its contents one level in,
// while the arrow stays on the pattern's line. Once the arrow moves down — the author's break before it
// or the margin's — the arrow is one level in and the body with it. Skala put the `(` one level past
// the arm on every row that holds, because the group before the arrow owns the arm's level and never held it.
public class SwitchArmChoppedParenthesis {
    object EveryBodyAnArrowHolds(int k) =>
        k switch {
            1 =>
            (
                a).C(),
            2 =>
            (
                a)[0][1],
            3 =>
            (
                a)?.B(),
            4 =>
            (
                a).B.C(),
            5 =>
            (
                a ?? b)!.C(),
            6 =>
            (
                a, b),
            7 =>
            (
                a)
                ? b
                : c,
            _ =>
            (
                a).C()
        };

    object UnderAWhenANestedSwitchAndAReturn(int k) {
        return k switch {
            1 when k > 0 =>
            (
                a).C(),
            { } =>
            (
                a, b) switch {
                _ => a
            },
            _ => k switch {
                2 =>
                (
                    a)[0],
                _ => null
            }
        };
    }

    object UnderALambdaAndAnArgument(int k) {
        System.Func<int, object> f = x => x switch {
            1 =>
            (
                a).C(),
            _ => null
        };
        return M(
            k switch {
                1 =>
                (
                    a).C(),
                _ => null
            }
        );
    }

    object AnArrowTheAuthorMovedDown(int k) =>
        k switch {
            1
                => (
                    a).C(),
            2
                =>
                (
                    a).C(),
            3 when k > 0
                =>
                (
                    a, b),
            _ => null
        };

    object AnArrowTheMarginMovesDown(int k) =>
        k switch {
            C.NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN =>
            (
                a).C(),
            C.MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM
                =>
                (
                    a).C(),
            C.GGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGG =>
            (
                a).C(),
            _ => null
        };

    object M(object o) => o;

    object a, b, c;
}
