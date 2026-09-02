using System;

// A record's primary constructor has no body, so there is nothing for this rule to look at — but a
// record may also declare an ordinary constructor next to one, chained through `: this(…)`, and the
// compiler-generated members around it (`<Clone>$`, the copy constructor, `Equals`) are where an
// analyzer that quietly assumed a plain class falls over. ⚠ A crashed analyzer passes every negative
// fixture, so a file like this one is the only way that failure is visible from a green run.
public sealed class Feed {
    public event EventHandler? Changed;

    public void Publish() => Changed?.Invoke(this, EventArgs.Empty);
}

public record Reading(int Value) {
    readonly Feed feed = new();

    public Reading(int value, Feed feed) : this(value) {
        this.feed = feed;
        feed.Changed += OnChanged;
    }

    public void Publish() => feed.Publish();

    void OnChanged(object? sender, EventArgs e) { }
}
