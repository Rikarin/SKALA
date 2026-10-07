// The type goes between `new` and the argument list; the arguments and the initializer stay as
// written: `_ = new Lease(30) { Owner = "pool" };`.
public sealed class Lease(int seconds) {
    public int Seconds { get; } = seconds;

    public string Owner { get; init; } = "";
}

public sealed class Pool {
    public void Warm() {
        Lease lease = new(30) { Owner = "pool" };
    }
}
