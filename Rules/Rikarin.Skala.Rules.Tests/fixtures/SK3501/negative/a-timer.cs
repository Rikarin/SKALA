using System.Threading;

public sealed class Heartbeat {
    // ⚠ Excluded by name. A timer disposed at the end of the method that started it never fires, so
    // the "fix" would delete the feature rather than repair a leak.
    public void Start() {
        var timer = new Timer(_ => { }, null, 0, 1000);
        timer.Change(0, 500);
    }
}
