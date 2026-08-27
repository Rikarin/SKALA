using System.IO;
using System.Threading.Tasks;

public sealed class Loader {
    // Adding `async` obliges the early return to be awaited too. It is a task of the method's own
    // type, so the rule can rewrite both — and reports only because it can.
    public Task<string> ReadAsync(string? path) {
        if (path is null) {
            return Task.FromResult(string.Empty);
        }

        using var reader = new StreamReader(path);
        return reader.ReadToEndAsync();
    }
}
