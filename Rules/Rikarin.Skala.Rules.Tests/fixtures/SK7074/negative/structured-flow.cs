// The same job the positive fixtures do with a jump, written with the structure the language has.
// `break`, `continue`, `return` and a label that nothing jumps to are not this rule's business.
public sealed class Work {
    public bool Run(int[][] rows) {
        foreach (var row in rows) {
            if (Broken(row)) {
                return false;
            }
        }

        return true;
    }

    static bool Broken(int[] row) {
        foreach (var cell in row) {
            if (cell < 0) {
                return true;
            }

            if (cell == 0) {
                continue;
            }

            break;
        }

        return false;
    }
}
