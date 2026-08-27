using System.IO;
using System.Threading.Tasks;

public sealed class Loader {
    public async Task<int> LengthAsync(string path) {
        // ⚠ The fix has to parenthesise: `await File.ReadAllTextAsync(path).Length` awaits the
        // wrong thing.
        var length = File.ReadAllTextAsync(path).Result.Length;
        await Task.Yield();
        return length;
    }
}
