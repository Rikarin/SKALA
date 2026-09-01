// ⚠ CS1997 forbids `return await` in an `async Task` method, so the body is a bare `await` and the
// edit has to put the `return` back.

using System.Threading.Tasks;

public sealed class Store {
    public async Task FlushAsync() {
        await WriteAsync();
    }

    static Task WriteAsync() => Task.CompletedTask;
}
