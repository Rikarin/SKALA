using System.IO;
using System.Threading.Tasks;

public sealed class Reader {
    // The repaired form. The `using` outlives the operation because the operation is awaited.
    public async Task<string> ReadAsync(string path) {
        using (var reader = new StreamReader(path)) {
            return await reader.ReadToEndAsync();
        }
    }
}
