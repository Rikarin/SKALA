public sealed class Conditional {
    public static int Run() {
#if DEBUG
        int Work() => 8;
#else
        int Work() => 7;
#endif

        return Work();
    }
}
