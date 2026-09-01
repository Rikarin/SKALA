public sealed class Settings {
    readonly object gate = new();

    string name = string.Empty;

    public string Name {
        get {
            lock (gate) {
                return name;
            }
        }

        set => name = value;
    }

    public void Rename(string next) {
        lock (gate) {
            name = next;
        }
    }
}
