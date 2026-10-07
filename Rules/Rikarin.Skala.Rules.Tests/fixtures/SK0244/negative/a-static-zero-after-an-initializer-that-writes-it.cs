// ⚠ #412's audit: static initializers run in textual order. `Init` sets `Count` to 5 while `Ready` is
// initialized, and `= 0` puts it back; without it `Read` returns 5.
public static class Registry {
    static readonly bool Ready = Init();
    static int Count = 0;

    static bool Init() {
        Count = 5;
        return true;
    }

    public static int Read() => Ready ? Count : -1;

    public static void Bump() => Count++;
}

public static class Probe {
    public static int Run() => Registry.Read();
}
