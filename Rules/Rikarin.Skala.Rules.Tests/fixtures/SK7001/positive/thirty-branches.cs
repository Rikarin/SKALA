// Thirty `if` statements: thirty decision points, so cyclomatic complexity 31 against a default
// threshold of 25. Flat, so its cognitive complexity is only 30 — which is the pair of numbers the
// two rules exist to tell apart.
public sealed class ThirtyBranches {
    public static int Total(int[] values) {
        var total = 0;
        if (values[0] > 0) {
            total += 0;
        }

        if (values[1] > 1) {
            total += 1;
        }

        if (values[2] > 2) {
            total += 2;
        }

        if (values[3] > 3) {
            total += 3;
        }

        if (values[4] > 4) {
            total += 4;
        }

        if (values[5] > 5) {
            total += 5;
        }

        if (values[6] > 6) {
            total += 6;
        }

        if (values[7] > 7) {
            total += 7;
        }

        if (values[8] > 8) {
            total += 8;
        }

        if (values[9] > 9) {
            total += 9;
        }

        if (values[10] > 10) {
            total += 10;
        }

        if (values[11] > 11) {
            total += 11;
        }

        if (values[12] > 12) {
            total += 12;
        }

        if (values[13] > 13) {
            total += 13;
        }

        if (values[14] > 14) {
            total += 14;
        }

        if (values[15] > 15) {
            total += 15;
        }

        if (values[16] > 16) {
            total += 16;
        }

        if (values[17] > 17) {
            total += 17;
        }

        if (values[18] > 18) {
            total += 18;
        }

        if (values[19] > 19) {
            total += 19;
        }

        if (values[20] > 20) {
            total += 20;
        }

        if (values[21] > 21) {
            total += 21;
        }

        if (values[22] > 22) {
            total += 22;
        }

        if (values[23] > 23) {
            total += 23;
        }

        if (values[24] > 24) {
            total += 24;
        }

        if (values[25] > 25) {
            total += 25;
        }

        if (values[26] > 26) {
            total += 26;
        }

        if (values[27] > 27) {
            total += 27;
        }

        if (values[28] > 28) {
            total += 28;
        }

        if (values[29] > 29) {
            total += 29;
        }

        return total;
    }
}
