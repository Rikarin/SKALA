public sealed class Work {
    public int Run(int start) {
        var value = start;

    again:
        value--;
        if (value > 0) {
            goto again;
        }

        return value;
    }
}
