using System;

// `delegate { … }` converts to a delegate of any signature; a lambda would have to invent the
// target's parameters. Out of scope.
public sealed class Source {
    public event EventHandler Changed = delegate { };

    public void Raise() => Changed(this, EventArgs.Empty);
}
