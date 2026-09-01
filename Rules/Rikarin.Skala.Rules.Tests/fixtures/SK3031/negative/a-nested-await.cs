// CS4032: removing `async` would leave the inner `await` in a method that no longer has it.

using System.Threading.Tasks;

public sealed class Store {
    public async Task<int> CountAsync() {
        return await ReadAsync(await OpenAsync());
    }

    static Task<int> ReadAsync(int handle) => Task.FromResult(handle);

    static Task<int> OpenAsync() => Task.FromResult(1);
}
