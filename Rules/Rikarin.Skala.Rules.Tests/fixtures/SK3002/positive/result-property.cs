using System.IO;
using System.Threading.Tasks;

public sealed class Loader {
    public async Task<int> LoadAsync(string path) {
        var text = File.ReadAllTextAsync(path).Result;
        await Task.Yield();
        return text.Length;
    }
}
