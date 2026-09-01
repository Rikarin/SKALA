using System;

namespace Microsoft.Extensions.Logging;

public interface ILogger { }

public sealed class Service {
    readonly ILogger logger;

    public Service(ILogger logger) => this.logger = logger;
}

// A logger somewhere in the file is not a logger in scope here. The walk is the enclosing method's
// parameters and the enclosing type's members, and nothing wider.
public sealed class Banner {
    public void Print() => Console.WriteLine("skala 1.3");
}
