using System;

// A cast changes the spelling of the anonymous function, not its identity.
public sealed class Pipeline {
    Action? work;

    public void Detach() {
        work -= (Action)(() => Console.WriteLine("never removed"));
    }

    public void Run() => work?.Invoke();
}
