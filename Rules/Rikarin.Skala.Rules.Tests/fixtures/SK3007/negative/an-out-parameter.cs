using System.IO;
using System.Threading.Tasks;

public sealed class Sizer {
    // CS1988: an async method may not declare a `ref`, `out` or `in` parameter. Adding `async` here
    // produces a fix that parses and does not compile, which is the one failure a fixing tool may
    // not have.
    public Task<string> ReadAsync(string path, out int size) {
        size = 0;
        using (var reader = new StreamReader(path)) {
            return reader.ReadToEndAsync();
        }
    }
}
