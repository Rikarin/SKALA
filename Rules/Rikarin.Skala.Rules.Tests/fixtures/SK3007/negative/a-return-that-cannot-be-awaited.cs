using System.IO;
using System.Threading.Tasks;

public sealed class Guarded {
    // ⚠ Every return is rewritten or none is. `null` is a task this method may return and is not one
    // the rule can put an `await` in front of, so the finding is withheld rather than half-fixed.
    public Task<string> ReadAsync(string? path) {
        if (path is null) {
            return null!;
        }

        using (var reader = new StreamReader(path)) {
            return reader.ReadToEndAsync();
        }
    }
}
