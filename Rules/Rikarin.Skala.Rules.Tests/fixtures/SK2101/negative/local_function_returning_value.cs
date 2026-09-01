using System.Diagnostics.Contracts;

static class Work {
    public static int Run() {
        [Pure]
        static int Twice(int value) => value * 2;

        return Twice(21);
    }
}
