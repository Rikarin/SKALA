using System;

// The event's owner was handed in by the caller. Whether it outlives this object is the caller's
// business and is not visible from here — but it is somebody's object rather than process-wide
// state, so it is not the destination this rule is about. Dependency injection makes this shape as
// common as the field one.
public sealed class Feed {
    public event EventHandler? Changed;

    public void Publish() => Changed?.Invoke(this, EventArgs.Empty);
}

public sealed class View {
    int changes;

    public View(Feed feed) {
        feed.Changed += OnChanged;
    }

    public int Changes => changes;

    void OnChanged(object? sender, EventArgs e) => changes++;
}
