using System;

public sealed class Source {
    public event EventHandler? Changed;

    public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}

public sealed class View {
    public void Detach(Source source) {
        source.Changed -= (s, e) => Redraw();
    }

    static void Redraw() {
    }
}
