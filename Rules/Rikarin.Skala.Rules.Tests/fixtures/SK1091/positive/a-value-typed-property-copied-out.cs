// A struct property that is only assigned and read whole: a copy of a copy and a copy of the field are
// the same value. Pinned by Probe (#425).
public struct Counter {
    public int N;
}

public sealed class Holder {
    private Counter Tally { get; set; }

    public int Bump() {
        var local = Tally;
        local.N += 2;
        Tally = local;
        return Tally.N;
    }
}

public static class Probe {
    public static int Run() => new Holder().Bump();
}
