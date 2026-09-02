using System;

static class Emit {
    // The documented way to ask this question. The rule reads the receiver's *static* type and does
    // not look through the cast, which is what makes this the escape hatch.
    public static Type Runtime(Type contract) => ((object)contract).GetType();
}
