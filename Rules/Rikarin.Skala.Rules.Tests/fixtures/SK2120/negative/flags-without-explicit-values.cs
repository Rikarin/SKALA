// ⚠ **The only shape in which the `[Flags]` guard is load-bearing, and it was missing.** A sabotage
// that inverted `flags is not null` turned nothing red: every other `[Flags]` fixture also writes its
// values down, so the numbering guard declined those enums first and the attribute guard was never
// reached. Two guards masking each other reads exactly like one guard working.
//
// A `[Flags]` enum left to the compiler to number is legal and correct — 0, 1, 2 are a valid zero
// and two valid bits, CA2217 is satisfied, and `Write | Execute` is 3, which means both. The
// attribute is the author saying so, and it is the only thing that separates this file from
// positive/or-operator.cs.
using System;

[Flags]
enum Access {
    None,
    Write,
    Execute
}

sealed class Gate {
    public Access Combine(Access left, Access right) => left | right;

    public bool Allows(Access held, Access wanted) => (held & wanted) == wanted;

    public Access Invert(Access held) => ~held;
}
