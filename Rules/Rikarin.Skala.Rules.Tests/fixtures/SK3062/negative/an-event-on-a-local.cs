using System;

// The event's owner was made inside the constructor and nothing outside can reach it, which is the
// clearest case of a subscription that cannot outlive the object. If the rule ever reports a local
// receiver it has stopped asking "who else can see this" and started asking "is there a `+=` here".
public sealed class Timer {
    public event EventHandler? Elapsed;

    public void Fire() => Elapsed?.Invoke(this, EventArgs.Empty);
}

public sealed class Job {
    int ticks;

    public Job() {
        var timer = new Timer();
        timer.Elapsed += OnElapsed;
        timer.Fire();
    }

    public int Ticks => ticks;

    void OnElapsed(object? sender, EventArgs e) => ticks++;
}
