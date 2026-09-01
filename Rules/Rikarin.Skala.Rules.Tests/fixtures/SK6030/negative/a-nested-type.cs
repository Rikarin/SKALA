namespace Contoso.Configuration;

public sealed class Settings {
    public sealed class Section {
        public string Name { get; init; } = string.Empty;
    }

    public enum Kind {
        None = 0
    }

    public delegate void Changed(Section section);
}
