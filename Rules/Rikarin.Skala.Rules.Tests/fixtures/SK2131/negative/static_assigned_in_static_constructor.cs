// A static get-only property is assignable from the static constructor, and this one is.
static class Machine {
    public static int Cores { get; }

    static Machine() => Cores = 8;
}
