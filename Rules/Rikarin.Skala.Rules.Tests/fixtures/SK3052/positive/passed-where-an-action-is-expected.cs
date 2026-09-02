using System;
using System.Threading.Tasks;

// ⚠ Nothing here says `void`. The lambda takes its return type from `Action`, so this is `async
// void` and `OnEviction` returns at the first `await`.
public sealed class Cache {
    public void OnEviction(Action callback) => callback();

    public void Wire(Cache cache) {
        cache.OnEviction(async () => await FlushAsync());
    }

    static Task FlushAsync() => Task.CompletedTask;
}
