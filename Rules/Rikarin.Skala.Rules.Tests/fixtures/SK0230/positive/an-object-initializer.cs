public sealed class Options {
    public int Depth { get; set; }
}

public static class Defaults {
    // No argument list, so the fix has to grow one: `new Options` is not an expression.
    public static Options Create() => new Options { };
}
