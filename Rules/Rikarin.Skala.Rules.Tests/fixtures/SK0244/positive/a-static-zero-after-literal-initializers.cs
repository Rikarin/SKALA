// Every static initializer above `Count` is a literal, so nothing can have written it before its own
// `= 0` runs, and the field was already zero.
public static class Registry {
    static readonly int Start = 3;
    static int Count = 0;

    public static int Read() => Start + Count;

    public static void Bump() => Count++;
}

public static class Probe {
    public static int Run() {
        Registry.Bump();
        return Registry.Read();
    }
}
