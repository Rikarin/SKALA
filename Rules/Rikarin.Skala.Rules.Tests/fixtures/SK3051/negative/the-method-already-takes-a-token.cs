using System.IO;
using System.Threading;
using System.Threading.Tasks;

// ⚠ The parameter is named `token` and not `cancellationToken` on purpose. With the conventional
// name the CS0100 guard silences this rule first, and the fixture would pass green while proving
// nothing at all about the token count — which is what it was doing until a sabotage of the count
// turned nothing red.
//
// A token in scope at the call is SK3004's shape and none is this rule's. That is what makes the
// two disjoint on any one call, and it is what this file is here to pin.
public sealed class Loader {
    public async Task<string> LoadAsync(string path, CancellationToken token) {
        return await File.ReadAllTextAsync(path);
    }
}
