// ⚠ A sequence of like operators is one increment, however long it is: `a && b && c && d` costs the
// same as `a && b`. Five flat conditions, three of them four operands long: 5 for the `if`s and 5
// for the sequences, so 10.
public sealed class LikeOperatorSequences {
    public static int Count(bool a, bool b, bool c, bool d) {
        var total = 0;
        if (a && b && c && d) {
            total++;
        }

        if (a || b || c || d) {
            total++;
        }

        if (a && b && c) {
            total++;
        }

        if (a || b) {
            total++;
        }

        if (c && d) {
            total++;
        }

        return total;
    }
}
