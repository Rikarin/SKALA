using System;

// ⚠ `+=` with a lambda is the overwhelmingly common and usually correct case: a subscription that
// lives exactly as long as the subscriber. Whether it *should* be undone is not decidable, and this
// rule reports only the `-=` that provably cannot undo one.
public sealed class Source {
    public event EventHandler? Changed;

    public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}

public sealed class View {
    public void Attach(Source source) {
        source.Changed += (s, e) => Redraw();
    }

    static void Redraw() {
    }
}
