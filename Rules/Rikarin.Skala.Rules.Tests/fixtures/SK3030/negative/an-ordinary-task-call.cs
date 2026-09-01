using System.Threading.Tasks;

public sealed class Pipeline {
    public async Task RunAsync() {
        ProcessAsync();
        await Task.Yield();
    }

    static async Task ProcessAsync() {
        await Task.Yield();
    }
}
