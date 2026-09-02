// A user-defined `>` is somebody else's semantics, and rewriting it to `>=` would be rewriting an
// operator this rule has not read.
//
// ⚠ The `OperatorMethod: null, IsLifted: false` guard is unreachable, and a sabotage proved it:
// removing it left this file green. Two things have to hold before the guard is consulted — the
// search must return `System.Int32` and the constant must be the literal `0` — and two `int`
// operands always select the *built-in* `>`, never a user-defined one and never a lifted one. There
// is no program in which the guard is the thing that declines a report. It stays as intent, the way
// SK2053 keeps its own `IsLifted` clause, and this file documents the shape rather than the guard.
class Position {
    public static bool operator >(Position left, int right) => true;

    public static bool operator <(Position left, int right) => false;
}

class C {
    bool Compare(Position position) => position > 0;
}
