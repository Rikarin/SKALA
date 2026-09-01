namespace Contoso.Design;

public struct Meter {
    public double Value;

    public readonly double Scaled(double factor) => Value * Ratio(factor);

    static double Ratio(double factor) => 1.0;
}
