public sealed record Settings(string Host, int Port, int Retries);

public sealed class Builder {
    public Settings Mix(Settings left, Settings right, int retries) =>
        new Settings(left.Host, right.Port, retries);
}
