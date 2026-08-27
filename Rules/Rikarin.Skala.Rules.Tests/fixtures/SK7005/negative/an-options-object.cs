// rules.json's SK7005 `good` example: the wide list replaced by one parameter. The options type
// itself has seven members and nothing fires on it either.
public readonly struct ConfigureOptions {
    public int Width { get; init; }

    public int Height { get; init; }

    public int Depth { get; init; }

    public int Left { get; init; }

    public int Top { get; init; }

    public bool Visible { get; init; }

    public string? Name { get; init; }
}

public sealed class AnOptionsObject {
    public static int Configure(in ConfigureOptions options) => options.Width;
}
