// ⚠ CS8509 and CS8524 own the switch expression, so SK2009 stood down on the form entirely
// (#280, ADR-008: host, never rebuild). Verified on a scratch project rather than recalled:
// `k switch { K.A => 1, K.B => 2 }` draws "warning CS8509: The switch expression does not handle
// all possible values of its input type … the pattern 'K.C' is not covered", and the identical
// switch written as a statement draws nothing from the compiler at all.
//
// The expression below omits two of three values and would have been reported before the change.
// #pragma is what keeps the fixture compiling: the point is that the *compiler* speaks here, and
// asserting SK2009's silence would be worth nothing if the file could not be built.

#pragma warning disable CS8509

enum Signal {
    Green,
    Amber,
    Red
}

sealed class Lights {
    public int Delay(Signal signal) =>
        signal switch {
            Signal.Green => 0,
            Signal.Amber => 3
        };
}
