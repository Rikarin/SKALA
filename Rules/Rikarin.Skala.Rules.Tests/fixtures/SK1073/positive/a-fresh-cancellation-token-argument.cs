using System.Threading;
using System.Threading.Tasks;

public sealed class Runner {
    public Task Start() => Work(new CancellationToken());

    static Task Work(CancellationToken cancellation) => Task.CompletedTask;
}
