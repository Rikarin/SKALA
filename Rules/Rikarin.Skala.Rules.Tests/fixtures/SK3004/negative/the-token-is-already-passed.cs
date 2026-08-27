using System.IO;
using System.Threading;
using System.Threading.Tasks;

public sealed class Loader {
    public async Task<int> LoadAsync(string path, CancellationToken cancellationToken) {
        var text = await File.ReadAllTextAsync(path, cancellationToken);
        await Task.Delay(250, cancellationToken);
        return text.Length;
    }
}
