// ⚠ #431: the constructor reads the field through a property before it overwrites it. The getter is a
// method call that never spells the field's name here. Measured `71` before the fix and `1` after.
public sealed class Meter {
    int level = 70;

    public int Before { get; }

    public Meter() {
        var seen = Level;
        level = 1;
        Before = seen;
    }

    int Level => level;

    public int Now => level;
}

public static class Probe {
    public static int Run() {
        var meter = new Meter();
        return meter.Before + meter.Now;
    }
}
