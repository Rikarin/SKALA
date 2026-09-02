using System;

// The repair the rule wants: the lambda is named once and both sides use the same instance.
public sealed class Source {
    public event EventHandler? Changed;

    public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}

public sealed class View {
    readonly EventHandler handler;

    public View() => handler = (s, e) => Redraw();

    public void Attach(Source source) => source.Changed += handler;

    public void Detach(Source source) => source.Changed -= handler;

    static void Redraw() {
    }
}
