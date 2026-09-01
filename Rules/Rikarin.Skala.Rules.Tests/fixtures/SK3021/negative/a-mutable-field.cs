using System.Threading;

public sealed class Counter {
    // The repaired shape: every caller operates on the real lock.
    SpinLock gate = new(false);

    int value;

    public void Increment() {
        var taken = false;
        gate.Enter(ref taken);
        value++;
        if (taken) {
            gate.Exit();
        }
    }
}
