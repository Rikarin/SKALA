public sealed class Settings {
    public string? Prefix { get; init; }

    public bool Unset => Prefix is null || Prefix == string.Empty;
}
