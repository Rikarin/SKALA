using System;

// The logger interface is matched on its own name, not on a namespace: the namespace is the part
// that differs across Microsoft.Extensions.Logging, Serilog, NLog and log4net.
public interface ILog { }

public abstract class ServiceBase {
    protected ILog Log { get; } = null!;
}

public sealed class Scheduler : ServiceBase {
    public void Tick() => Console.Write("tick");
}
