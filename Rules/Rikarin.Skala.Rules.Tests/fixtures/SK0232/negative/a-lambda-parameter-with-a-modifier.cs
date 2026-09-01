public delegate int Adjust(ref int value);

public static class Modified {
    public static int Run() {
        Adjust adjust = (ref int n) => n + 1;
        var value = 1;
        return adjust(ref value);
    }
}
