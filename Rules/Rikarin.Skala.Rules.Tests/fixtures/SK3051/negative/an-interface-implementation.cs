using System.Threading.Tasks;

public interface IJob {
    Task RunAsync();
}

public sealed class Nightly : IJob {
    public async Task RunAsync() {
        await Task.Delay(5);
    }
}
