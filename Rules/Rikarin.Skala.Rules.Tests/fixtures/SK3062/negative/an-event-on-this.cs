using System;

// Subscribing to one's own event publishes nothing: the object that will raise the event is the
// object being built, and no second reader exists. ⚠ It is declined by the same receiver gate that
// declines a field rather than by a special case for `this` — `this` is not static state, so the
// gate never reaches a decision about it.
public sealed class Document {
    int loads;

    public Document() {
        this.Loaded += OnLoaded;
    }

    public event EventHandler? Loaded;

    public int Loads => loads;

    public void Load() => Loaded?.Invoke(this, EventArgs.Empty);

    void OnLoaded(object? sender, EventArgs e) => loads++;
}
