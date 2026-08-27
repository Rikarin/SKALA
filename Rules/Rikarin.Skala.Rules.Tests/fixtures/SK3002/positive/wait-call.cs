using System.Threading.Tasks;

public sealed class Runner {
    public async Task RunAsync(Task work) {
        work.Wait();
        await Task.Yield();
    }
}
