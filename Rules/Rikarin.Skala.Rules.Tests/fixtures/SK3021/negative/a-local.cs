using System.Threading;

public sealed class Counter {
    // A local is not a field and has no `readonly` to remove; it is also the shape that works.
    public bool Try() {
        var gate = new SpinLock(false);
        var taken = false;
        gate.Enter(ref taken);
        if (taken) {
            gate.Exit();
        }

        return taken;
    }
}
