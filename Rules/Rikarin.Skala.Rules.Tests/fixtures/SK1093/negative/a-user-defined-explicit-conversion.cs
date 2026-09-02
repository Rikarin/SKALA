public readonly struct Metres {
    public Metres(double value) => Value = value;

    public double Value { get; }

    public static explicit operator Metres(double value) => new Metres(value);
}

public sealed class Conversions {
    public Metres Get(double raw) {
        var length = (Metres)raw;
        return length;
    }
}
