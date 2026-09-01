using System.IO;
using System.Threading.Tasks;

public sealed class Store {
    public async Task<int> CountAsync() {
        try {
            return await LoadAsync();
        } catch (IOException) {
            return 0;
        }
    }

    static Task<int> LoadAsync() => Task.FromResult(1);
}
