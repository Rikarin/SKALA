// ⚠ #423: `(a / b) & 0` is always 0 when it completes, and it does not complete when `b` is 0.
public static class Probe {
    static int Masked(int a, int b) => (a / b) & 0;

    public static int Run() => Masked(1, 0);
}
