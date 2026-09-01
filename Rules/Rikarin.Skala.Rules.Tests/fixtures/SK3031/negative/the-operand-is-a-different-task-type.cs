// `await` converts a `ValueTask<int>` into an `int`; `return` does not convert it into a `Task<int>`.

using System.Threading.Tasks;

public sealed class Store {
    public async Task<int> CountAsync() {
        return await LoadAsync();
    }

    static ValueTask<int> LoadAsync() => new(1);
}
