// `ref` reads the variable as well as writing it, so there is nothing to inline.
public sealed class Reader {
    static void Bump(ref int value) => value++;

    public int Run() {
        int value = 0;
        Bump(ref value);
        return value;
    }
}
