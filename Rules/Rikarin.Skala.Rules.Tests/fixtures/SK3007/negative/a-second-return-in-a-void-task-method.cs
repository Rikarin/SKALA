using System.IO;
using System.Threading.Tasks;

public sealed class Appender {
    // The non-generic form drops the `return` keyword, which only works where the return was going
    // to fall off the end anyway. A second return means it was not.
    public Task WriteAsync(string path, string text) {
        using var writer = new StreamWriter(path);
        if (text.Length > 0) {
            return writer.WriteAsync(text);
        }

        return Task.CompletedTask;
    }
}
