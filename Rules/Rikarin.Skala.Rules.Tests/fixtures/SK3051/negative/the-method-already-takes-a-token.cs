using System.IO;
using System.Threading;
using System.Threading.Tasks;

// ⚠ Exactly one token in scope is SK3004's shape, and none is this rule's. The count is what makes
// the two disjoint, and no body can satisfy both.
public sealed class Loader {
    public async Task<string> LoadAsync(string path, CancellationToken cancellationToken) {
        return await File.ReadAllTextAsync(path);
    }
}
