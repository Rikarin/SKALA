// `nameof` produces a compile-time string and reads no storage at all.
static class Config {
    public static readonly string Name = nameof(Later);

    public static readonly int Later = 42;
}
