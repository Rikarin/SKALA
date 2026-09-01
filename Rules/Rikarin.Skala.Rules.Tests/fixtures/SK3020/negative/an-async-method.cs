using System.Threading.Tasks;

public sealed class Loader {
    // The compiler wraps the result, so this returns a real task carrying a null string. That is
    // an ordinary nullable-reference question, not a null task.
    public async Task<string?> Read(bool cached) {
        await Task.Yield();
        return cached ? null : "value";
    }
}
