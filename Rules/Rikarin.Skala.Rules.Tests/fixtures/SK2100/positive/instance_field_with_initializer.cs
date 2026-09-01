using System;

// Both halves are true of this declaration and it is one defect, so it is reported once — as an
// instance field, because the initializer is beside the point when the attribute is already inert.
sealed class Counter {
    [ThreadStatic] int seen = 7;

    public int Seen => seen;
}
