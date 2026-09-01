using System;

public sealed class Matrix {
    // ⚠ Below three characters one edit is most of the name: `i` and `j` are one apart, and so is
    // every other pair of short parameters. The rule compares nothing shorter.
    public int At(int i, int j) {
        if (i < 0 || j < 0) {
            throw new ArgumentOutOfRangeException("k");
        }

        return i * j;
    }
}
