using System.Threading;

public sealed class Counter {
    readonly SpinLock gate = new(false);

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
