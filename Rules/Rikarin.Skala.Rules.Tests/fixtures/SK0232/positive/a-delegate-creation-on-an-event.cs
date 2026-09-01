using System;

public sealed class Source {
    public event EventHandler? Changed;

    public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}

public sealed class Listener {
    public void Attach(Source source) => source.Changed += new EventHandler(OnChanged);

    void OnChanged(object? sender, EventArgs e) { }
}
