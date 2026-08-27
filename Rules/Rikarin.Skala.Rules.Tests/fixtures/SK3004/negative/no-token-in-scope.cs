using System.IO;
using System.Threading.Tasks;

public sealed class Reader {
    // Nothing to forward. A method that never accepted a token cannot have failed to pass one on.
    public async Task<int> ReadAsync(string path) {
        var text = await File.ReadAllTextAsync(path);
        return text.Length;
    }
}
