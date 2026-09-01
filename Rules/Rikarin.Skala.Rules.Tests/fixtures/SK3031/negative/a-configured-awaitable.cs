// `ConfigureAwait` produces an awaitable that is not a task, so there is nothing to return directly.

using System.Threading.Tasks;

public sealed class Store {
    public async Task<int> CountAsync() {
        return await LoadAsync().ConfigureAwait(false);
    }

    static Task<int> LoadAsync() => Task.FromResult(1);
}
