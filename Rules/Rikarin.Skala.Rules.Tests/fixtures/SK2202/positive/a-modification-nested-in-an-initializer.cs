using System;

// The initializer member itself is not a modification; the `++` inside its value is, and it is
// skipped whenever the receiver is null.
public sealed class Progress {
    public int Seen { get; init; }
}

public sealed class Importer {
    int seen;

    public void Report(Action<Progress>? sink) {
        sink?.Invoke(new Progress { Seen = ++seen });
    }
}
