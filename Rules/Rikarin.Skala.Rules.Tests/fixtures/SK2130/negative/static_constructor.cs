// ⚠ The static constructor runs *after* every field initializer, so a read from it sees a fully
// initialized type no matter where the field is written down.
static class Config {
    public static readonly int Value;

    static Config() => Value = Later;

    public static readonly int Later = 42;
}
