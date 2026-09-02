using System.IO;

public static class Store {
    public static void Publish(string path, string text) => File.WriteAllText(path, text);
}
