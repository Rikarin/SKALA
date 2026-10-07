// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
namespace Constructs.Breaks;

// SK-DIV-0200 (issue #435). After a block comment that spans lines, the break point of an `=`, a
// compound assignment, a lambda's or a switch arm's arrow and a named argument's colon is not taken:
// the value stays on the comment's last line, and a value too long for it wraps inside itself, nested
// from the statement's line as if the comment were one line. A chopped list, an operator broken after
// its operand and a closer still break after such a comment. Before this file no construct held a
// comment that spans lines in front of a value.
public class MultiLineCommentBeforeAValue {
    int field = /* a
      b */ 1;

    int Property { get; } = /* a
      b */ 1;

    int Body => /* a
      b */ 1;

    void Default(
        int value = /* a
          b */ 1
    ) { }

    void Values() {
        int local = /* gap
                       gap2 */ 1;
        local += /* a
          b */ 1;
        local = /* a
          b */ 1;
        Func<int> lambda = () => /* a
          b */ 1;
        var arm = local switch {
            1 => /* a
              b */ 2,
            _ => 3
        };
        Named(
            value: /* a
              b */ 1
        );
        int written = /* a
          b */
            1;
        int before =
            /* a
            b */ 1;
    }

    void Wraps() {
        int sum = /* a
          b */ alphaArgumentValueNumberOne
            + betaArgumentValueNumberTwo
            + gammaArgumentValueNumberThreeAndMoreAndMoreStill;
        int call = /* a
          b */ Compute(
            alphaArgumentValueNumberOne,
            betaArgumentValueNumberTwo,
            gammaArgumentValueNumberThreeAndMoreStill
        );
        var chain = /* a
          b */ Compute(alphaArgumentValueNumberOne)
            .Then(betaArgumentValueNumberTwo)
            .Then(gammaArgumentValueNumberThreeAndMore);
        var arms = /* a
          b */ field switch {
            1 => 2,
            _ => 3
        };
    }

    void StillBreaks() {
        Compute(
            1, /* a
              b */
            2
        );
        int operand = alpha /* a
          b */
            + beta;
        var fill = new int[] {
            1, /* a
              b */ 2
        };
    }
}
