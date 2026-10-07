// ⚠ #423: `(Weight)4` calls an operator somebody wrote, which the audit measured running before the
// fix and not after. A cast is not free because it is spelled like one.
public struct Weight {
    public static int Conversions;

    public int Value;

    public static explicit operator Weight(int value) {
        Conversions++;
        return new Weight { Value = value };
    }
}

public sealed class Parcel {
    Weight weight = (Weight)4;

    public Parcel() {
        weight = default;
    }

    public int Total => weight.Value;
}

public static class Probe {
    public static int Run() => new Parcel().Total * 10 + Weight.Conversions;
}
