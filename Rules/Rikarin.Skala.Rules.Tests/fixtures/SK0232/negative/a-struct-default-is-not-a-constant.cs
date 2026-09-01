using System.Threading;

public static class Cancellable {
    static int Wait(string name, CancellationToken cancellation = default) =>
        cancellation.IsCancellationRequested ? 0 : name.Length;

    public static int Run(string name) => Wait(name, default);
}
