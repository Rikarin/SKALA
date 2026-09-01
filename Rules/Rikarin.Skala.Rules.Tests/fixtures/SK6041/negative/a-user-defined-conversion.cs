using System.Collections.Generic;

public readonly struct Celsius {
    public Celsius(double degrees) => Degrees = degrees;

    public double Degrees { get; }

    public static implicit operator Fahrenheit(Celsius value) => new(value.Degrees * 1.8 + 32);
}

public readonly struct Fahrenheit {
    public Fahrenheit(double degrees) => Degrees = degrees;

    public double Degrees { get; }
}

public static class Temperatures {
    public static double Hottest(List<Celsius> readings) {
        var hottest = double.MinValue;
        foreach (Fahrenheit reading in readings) {
            if (reading.Degrees > hottest) {
                hottest = reading.Degrees;
            }
        }

        return hottest;
    }
}
