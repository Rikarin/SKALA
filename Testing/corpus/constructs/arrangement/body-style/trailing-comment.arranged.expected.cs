// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaCleanup generated=2026-10-08
namespace Skala.Corpus.Arrangement;

// #463 / SK-DIV-0086: a `//` comment trailing the only statement does not keep the block. The oracle
// converts each of these at both values of skala_use_heuristics_for_body_style and leaves the comment
// at the end of the line, behind the new semicolon.
//
// ⚠ No row with a comment on a line of its own above the statement, and not because it stays a
// block: at the export the oracle converts `{ // c ⏎ return 4; }` too, writing the comment between
// the `=>` and the value, and Skala declines that placement (SK-DIV-0086's deliberate half).
public class TrailingComment {
    int _n;

    public int Return() => 1; // trailing a return

    public int Spaced() => 2; // the spaces before it are the formatter's

    public int Multi(int a, int b) =>
        a
        + b; // trailing a value that spans two lines

    public int Getter => _n; // trailing a getter that collapses onto its property

    public int Arrow => _n; // trailing a getter that is already an arrow

    public int Both {
        get => _n;
        set => _n = value; // trailing a setter
    }

    public static TrailingComment operator +(TrailingComment a, TrailingComment b) => a; // trailing an operator

    public int Local() {
        int Inner() => 3; // trailing a local function

        return Inner();
    }

    // A void method with a trailing comment is still a void method: the heuristic keeps it a block.
    public void Expression() {
        Console.WriteLine("x"); // trailing an expression statement
    }
}
