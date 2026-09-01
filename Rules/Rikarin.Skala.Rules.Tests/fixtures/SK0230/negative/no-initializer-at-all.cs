public sealed class Options {
    public int Depth { get; set; }
}

public static class Defaults {
    public static Options Create() => new Options();
}
