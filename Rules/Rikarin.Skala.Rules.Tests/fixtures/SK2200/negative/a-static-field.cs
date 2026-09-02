// A static initializer runs once and an instance constructor writing over it is SK2134's concept,
// not this one.
public sealed class Totals {
    static int seen = 0;

    public Totals() {
        seen = 1;
    }

    public static int Seen => seen;
}
