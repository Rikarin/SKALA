using System.IO;
using System.Threading.Tasks;

public sealed class Loader {
    public async Task<int> LoadAsync(string path) {
        var text = await File.ReadAllTextAsync(path);
        return text.Length;
    }
}
