using System;

// A static field assigned from the static constructor rather than an initializer still runs once
// on one thread, but the rule is about the *initializer* and there is none to point at.
static class Late {
    [ThreadStatic] static int slot;

    public static void Fill(int value) => slot = value;

    public static int Slot => slot;
}
