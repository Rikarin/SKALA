namespace Contoso.Configuration {
    public sealed class Settings {
        public string Path { get; init; } = string.Empty;
    }

    public enum Severity {
        None = 0
    }
}
