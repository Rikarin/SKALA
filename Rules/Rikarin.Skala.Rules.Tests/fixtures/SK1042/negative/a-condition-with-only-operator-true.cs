// ⚠ #425: a type with `operator true` but no `&` can stand alone as a condition, and `bool && Flag` is
// CS0019 — measured for #412's audit.
public readonly struct Flag {
    public readonly bool Value;

    public Flag(bool value) => Value = value;

    public static bool operator true(Flag flag) => flag.Value;

    public static bool operator false(Flag flag) => !flag.Value;
}

public static class Holder {
    public static int Go(bool ready, Flag flag) {
        if (ready) {
            if (flag) {
                return 1;
            }
        }

        return 0;
    }
}
