using System;

// ⚠ Two guards decline this and the *first* one is not the obvious one. `System.Delegate` declares
// `operator ==`, so the null test is not the reference test `?.` performs and the rule stops there;
// the "only a member or an element access may follow the `?`" check that would otherwise stop
// `describe?()` from being spliced never runs, because nothing but a delegate is invocable.
public sealed class Reader {
    public string? Describe(Func<string>? describe) => describe != null ? describe() : null;
}
