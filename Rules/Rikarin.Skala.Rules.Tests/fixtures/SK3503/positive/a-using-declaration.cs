using System.IO;
using System.Threading.Tasks;

public sealed class Copier {
    public async Task CopyAsync(Stream source, string path) {
        using var target = new FileStream(path, FileMode.Create);
        await source.CopyToAsync(target);
    }
}
