using System.IO;
using System.Threading.Tasks;

public sealed class Direct {
    // No block, so no `using` and nothing to be returned out of one.
    public Task<string> ReadAsync(string path) => File.ReadAllTextAsync(path);
}
