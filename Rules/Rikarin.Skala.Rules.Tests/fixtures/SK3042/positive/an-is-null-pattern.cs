public sealed class Connection { }

public sealed class Pool {
    readonly object gate = new();

    Connection? shared;

    public Connection Get() {
        if (shared is null) {
            lock (gate) {
                if (shared is null) {
                    shared = new Connection();
                }
            }
        }

        return shared;
    }
}
