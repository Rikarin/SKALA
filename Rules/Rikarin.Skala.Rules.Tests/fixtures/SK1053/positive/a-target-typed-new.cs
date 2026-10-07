// #402: `_ = new();` is CS8754 — a discard has no type for `new()` to take. The fix moves the
// declared type into the creation, `_ = new Session();`, which calls the same constructor.
public sealed class Session { }

public sealed class Pool {
    public void Warm() {
        Session session = new();
    }
}
