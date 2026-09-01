using System;

public sealed class Settings {
    static readonly Lazy<Settings> Shared = new(static () => new Settings(), true);

    public static Settings Instance => Shared.Value;
}
