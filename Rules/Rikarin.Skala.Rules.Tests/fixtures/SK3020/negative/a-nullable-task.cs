using System.Threading.Tasks;

public sealed class Writer {
    // ⚠ `Task?` is the author saying null is a value this method returns; the contract already
    // carries the warning that an unguarded `await` would be wrong.
    public Task? Flush(bool dirty) {
        if (!dirty) {
            return null;
        }

        return Task.CompletedTask;
    }
}
