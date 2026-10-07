// The last link may be any property: `Total = Total + n` and `Total += n` both call the getter once
// and the setter once. Only the receiver is evaluated a different number of times (#423).
public sealed class Meter {
    int total;

    public int Calls;

    public int Total {
        get {
            Calls++;
            return total;
        }
        set {
            Calls += 10;
            total = value;
        }
    }

    public void Add(int n) {
        Total = Total + n;
    }
}

public static class Probe {
    public static int Run() {
        var meter = new Meter();
        meter.Add(3);
        return meter.Calls * 100 + meter.Total;
    }
}
