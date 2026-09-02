using System;
using System.Threading.Tasks;

// The same text against `Func<Task>` is correct, which is why the *converted* type decides.
public sealed class Wiring {
    public void Wire() {
        Func<Task> callback = async () => await Task.Yield();

        callback();
    }
}
