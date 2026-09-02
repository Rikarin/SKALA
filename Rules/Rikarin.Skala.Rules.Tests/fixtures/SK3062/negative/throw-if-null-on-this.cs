using System;

// `ArgumentNullException.ThrowIfNull(this)` is a static method reading its argument and storing it
// nowhere. ⚠ It is also close enough to shape B — a qualified call taking a bare `this` — that it is
// the fixture most likely to go red if the receiver gate is ever loosened from "the receiver binds
// to a static field or property" to "the receiver is spelled as something static".
public sealed class Guarded {
    readonly string name;

    public Guarded(string name) {
        ArgumentNullException.ThrowIfNull(this);
        this.name = name;
    }

    public string Name => name;
}
