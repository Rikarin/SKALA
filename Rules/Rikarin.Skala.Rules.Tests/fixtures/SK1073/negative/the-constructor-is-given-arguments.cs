using System;

public sealed class Registry {
    public Guid Parse(string text) => new Guid(text);

    public TimeSpan Minute() => new TimeSpan(0, 1, 0);
}
