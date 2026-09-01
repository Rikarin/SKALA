using System.IO;
using System.Threading.Tasks;

public sealed class Loader {
    // ⚠ The one shape the two rules overlap on. `SK3007` reports this and carries the fix —
    // `async` plus an `await` inside the block — so this rule stays quiet rather than doubling it.
    public Task<string> ReadAsync(string path) {
        using var reader = new StreamReader(path);
        using var pending = reader.ReadToEndAsync();
        return pending;
    }
}
