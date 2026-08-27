using System.IO;
using System.Threading.Tasks;

public sealed class Reader {
    public Task<string> ReadAsync(string path) {
        using (var reader = new StreamReader(path)) {
            return reader.ReadToEndAsync();
        }
    }
}
