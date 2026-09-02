// Shape A. The static slot belongs to `Tracker`, not to `Session`, so `SK2134` — which only ever
// looks at the constructor's own containing type — says nothing here and this is the only finding on
// the line. `Tracker.Current` outlives every session and is readable from any thread the instant the
// assignment retires, which is before `User` has been written.
public static class Tracker {
    public static Session? Current;
}

public sealed class Session {
    public Session(string user) {
        Tracker.Current = this;
        User = user;
    }

    public string User { get; }
}
