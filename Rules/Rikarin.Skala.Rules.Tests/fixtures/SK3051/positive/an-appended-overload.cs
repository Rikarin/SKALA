using System.Threading;
using System.Threading.Tasks;

public sealed class Store {
    public async Task SaveAsync(string key) {
        await WriteAsync(key);
    }

    static Task WriteAsync(string key) => Task.CompletedTask;

    static Task WriteAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;
}
