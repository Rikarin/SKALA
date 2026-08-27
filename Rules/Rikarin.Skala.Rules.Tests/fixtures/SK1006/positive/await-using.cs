using System.IO;
using System.Threading.Tasks;

public sealed class Writer {
    public async Task WriteAsync(string path) {
        await using (var stream = File.OpenWrite(path)) {
            await stream.WriteAsync(new byte[] { 0 });
        }
    }
}
