// ⚠ [#321]'s stand-down is narrow, and this is what keeps it narrow. Every member of `Side` is
// handled, so SK2009 has nothing to say with or without the `default:` section — and with nothing to
// contradict, the section really is what a `switch` with no matching section already does. The
// finding stands and the fix is offered.
//
// Without this fixture "SK0240 declines an empty default on an enum switch" would be indistinguishable
// from "SK0240 declines an empty default", which is a rule two thirds switched off.

enum Side {
    Left,
    Right
}

class C {
    public static void Run(Side side) {
        switch (side) {
            case Side.Left:
                Use(side);
                break;

            case Side.Right:
                Use(side);
                break;

            default:
                break;
        }
    }

    static void Use(Side side) { }
}
