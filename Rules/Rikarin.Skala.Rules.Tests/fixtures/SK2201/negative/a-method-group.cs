using System;

// Two delegates over the same target and method compare equal, so this removes what it named.
public sealed class Source {
    public event EventHandler? Changed;

    public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}

public sealed class View {
    public void Attach(Source source) => source.Changed += Redraw;

    public void Detach(Source source) => source.Changed -= Redraw;

    void Redraw(object? sender, EventArgs e) {
    }
}
