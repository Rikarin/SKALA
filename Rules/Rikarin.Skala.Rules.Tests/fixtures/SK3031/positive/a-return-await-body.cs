using System.Threading.Tasks;

public sealed class Store {
    public async Task<int> CountAsync() {
        return await LoadAsync();
    }

    static Task<int> LoadAsync() => Task.FromResult(1);
}
