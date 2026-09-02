public sealed class Limits {
    public int Maximum { get; } = 100;

    public bool Fits(int value) => value <= Maximum;
}
