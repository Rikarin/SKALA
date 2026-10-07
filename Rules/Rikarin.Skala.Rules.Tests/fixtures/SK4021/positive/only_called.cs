// Every use is a call, so no delegate carries the instance, and `nameof` makes none either.
public sealed class Widget {
    public string Describe() => nameof(Compute) + "=" + Compute(20);

    private int Compute(int value) => value + 22;
}

public static class Probe {
    public static string Run() => new Widget().Describe();
}
