using System.IO;
using System.Threading.Tasks;

public sealed class Writer {
    // The repaired form.
    public async Task WriteAsync(Stream target) {
        await using (var writer = new StreamWriter(target)) {
            await writer.WriteLineAsync("done");
        }
    }
}
