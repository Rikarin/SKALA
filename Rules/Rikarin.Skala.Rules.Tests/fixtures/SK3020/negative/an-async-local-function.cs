using System.Threading.Tasks;

public sealed class Loader {
    public Task<string?> Read(bool cached) {
        return Inner();

        async Task<string?> Inner() {
            await Task.Yield();
            return cached ? null : "value";
        }
    }
}
