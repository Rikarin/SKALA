using System.Threading;

public sealed class Runner {
    public bool Start(CancellationToken cancellation = default(CancellationToken)) =>
        cancellation.CanBeCanceled;
}
