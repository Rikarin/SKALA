using System.IO;
using System.Threading.Tasks;

public sealed class Writer {
    public async Task WriteAsync(Stream target) {
        using (var writer = new StreamWriter(target)) {
            await writer.WriteLineAsync("done");
        }
    }
}
