using System.IO;
using System.Threading;
using System.Threading.Tasks;

public sealed class Loader {
    public async Task<string> LoadAsync(string path) {
        var text = await File.ReadAllTextAsync(path);
        return text;
    }
}
