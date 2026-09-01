using System.Threading.Tasks;

public sealed class Store {
    public async Task<int> CountAsync() {
        var offset = 1;
        return await LoadAsync(offset);
    }

    static Task<int> LoadAsync(int offset) => Task.FromResult(offset);
}
