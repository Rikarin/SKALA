using System;

// The lambda is created once and named; the `-=` names the same instance.
public sealed class Pump {
    Action? work;

    public void Cycle() {
        Action step = () => Console.WriteLine("step");
        work += step;
        work?.Invoke();
        work -= step;
    }
}
