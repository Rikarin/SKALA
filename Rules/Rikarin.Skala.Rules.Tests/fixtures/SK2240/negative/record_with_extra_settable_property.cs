namespace Fixtures.SK2240;

public sealed record Config(int X, int Y) {
    public string? Name { get; set; }
}

public static class RecordWithExtraSettableProperty {
    public static Config Move(Config value, int x, int y) => value with { X = x, Y = y };
}
