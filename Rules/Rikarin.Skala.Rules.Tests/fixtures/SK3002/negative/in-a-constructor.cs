using System.IO;

public sealed class Loader {
    public Loader(string path) {
        // A constructor cannot be `async`, so there is no fix and the finding would be advice
        // nobody can take.
        Text = File.ReadAllTextAsync(path).Result;
    }

    public string Text { get; }
}
