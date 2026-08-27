using System.IO;
using System.Threading.Tasks;

public sealed class Counter {
    // ⚠ The `using` may still be wrong — a lock scope returned out of would be — but the rule
    // cannot prove that this task touches the resource, and guessing about ownership is how a rule
    // comes to report the correct code around the incorrect code.
    public Task<int> CountAsync(string path) {
        using (var reader = new StreamReader(path)) {
            return LoadAsync();
        }
    }

    static Task<int> LoadAsync() => Task.FromResult(0);
}
