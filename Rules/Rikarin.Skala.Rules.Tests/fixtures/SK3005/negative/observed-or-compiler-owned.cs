using System.Threading.Tasks;

sealed class Worker {
    public async Task StartAsync() {
        SaveAsync();
        await Task.Yield();
    }

    public void ExplicitlyDetached() {
        _ = SaveAsync();
    }

    public Task Forward() => SaveAsync();

    static Task SaveAsync() => Task.CompletedTask;
}
