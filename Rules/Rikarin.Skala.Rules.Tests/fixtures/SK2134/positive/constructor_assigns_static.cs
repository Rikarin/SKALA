// Every session overwrites the last one for every other session, on every thread. The line reads as
// per-object registration and is not.
sealed class Session {
    static Session? current;

    public Session(string user) {
        current = this;
        User = user;
    }

    public string User { get; }

    public static Session? Current => current;
}
