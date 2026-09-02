public sealed class Jumping {
    // ⚠ A `goto`'s label may sit anywhere in the member, including above the local function, so
    // "the block ends here" is not what the statement says. SK7074 owns unstructured `goto`.
    public static int Run(bool again) {
        var total = 0;

    retry:
        total++;

        if (total > 3) {
            return total;
        }

        int Step() => 1;

        goto retry;
    }
}
