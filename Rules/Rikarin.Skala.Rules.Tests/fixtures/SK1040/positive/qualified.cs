public sealed class Reading {
    System.Nullable<double> temperature = 21.5;

    public double Value => temperature ?? 0;
}
