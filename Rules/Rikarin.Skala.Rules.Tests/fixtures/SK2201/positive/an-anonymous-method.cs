using System;

public sealed class Feed {
    public event EventHandler? Arrived;

    public void Raise() => Arrived?.Invoke(this, EventArgs.Empty);
}

public sealed class Reader {
    public void Detach(Feed feed) {
        feed.Arrived -= delegate {
        };
    }
}
