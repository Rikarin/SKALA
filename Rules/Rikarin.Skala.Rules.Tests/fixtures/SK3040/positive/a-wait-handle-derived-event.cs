using System.Threading;

public sealed class Pump {
    readonly ManualResetEvent ready = new(false);

    int served;

    public void Serve() {
        // `ManualResetEvent` reaches the rule through `WaitHandle`, not by its own name.
        lock (ready) {
            served++;
        }
    }
}
