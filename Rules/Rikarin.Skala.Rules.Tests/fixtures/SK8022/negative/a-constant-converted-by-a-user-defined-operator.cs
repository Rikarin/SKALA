using Xunit;

// ⚠ #412's audit: `5` reaches `Equal<Meters>` through a user-defined conversion, which runs code. As
// written it runs after `Measure()`; swapped, it runs first, and the log reads the other way round.
public readonly struct Meters {
    public readonly int Value;

    public Meters(int value) => Value = value;

    public static implicit operator Meters(int value) {
        Probe.Log += "convert ";
        return new Meters(value);
    }
}

public static class Probe {
    public static string Log = "";

    static Meters Measure() {
        Log += "measure ";
        return new Meters(5);
    }

    public static string Run() {
        Log = "";
        Assert.Equal(Measure(), 5);
        return Log;
    }
}
