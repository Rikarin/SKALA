public sealed record Settings(string Host, int Port);

public sealed class Builder {
    public Settings Mix(Settings left, Settings right) => new Settings(left.Host, right.Port);
}
