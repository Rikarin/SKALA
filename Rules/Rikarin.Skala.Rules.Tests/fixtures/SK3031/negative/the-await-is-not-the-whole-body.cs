using System.Threading.Tasks;

public sealed class Store {
    public async Task<int> CountAsync(bool cached) {
        if (cached) {
            return await CachedAsync();
        }

        return await LoadAsync();
    }

    static Task<int> CachedAsync() => Task.FromResult(0);

    static Task<int> LoadAsync() => Task.FromResult(1);
}
