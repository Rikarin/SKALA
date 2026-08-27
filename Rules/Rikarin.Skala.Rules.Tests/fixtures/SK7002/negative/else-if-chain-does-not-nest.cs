// ⚠ Thirteen branches, cognitive complexity 13. The `else if` takes no nesting increment — "the
// mental cost has already been paid when reading the if" — so the chain costs one each. Written as
// thirteen nested `if`s the same conditions would score 91.
public sealed class ElseIfChainDoesNotNest {
    public static int Classify(int value) {
        if (value == 1) {
            return 1;
        } else if (value == 2) {
            return 2;
        } else if (value == 3) {
            return 3;
        } else if (value == 4) {
            return 4;
        } else if (value == 5) {
            return 5;
        } else if (value == 6) {
            return 6;
        } else if (value == 7) {
            return 7;
        } else if (value == 8) {
            return 8;
        } else if (value == 9) {
            return 9;
        } else if (value == 10) {
            return 10;
        } else if (value == 11) {
            return 11;
        } else if (value == 12) {
            return 12;
        } else {
            return 0;
        }
    }
}
