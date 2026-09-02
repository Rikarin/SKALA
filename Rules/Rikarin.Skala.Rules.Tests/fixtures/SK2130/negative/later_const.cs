// A `const` is substituted at compile time, so no ordering between the two exists.
static class Config {
    public static readonly int Value = Later;

    public const int Later = 42;
}
