using System;

// `f?()` is not syntax: only a member or an element access may follow the `?`.
public sealed class Reader {
    public string? Describe(Func<string>? describe) => describe != null ? describe() : null;
}
