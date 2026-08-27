using System.IO;

public sealed class Loader {
    static readonly string Text = File.ReadAllTextAsync("x").Result;

    public static int Length => Text.Length;
}
