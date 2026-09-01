using System;

namespace Microsoft.Extensions.Logging;

public interface ILogger { }

// Reading input is not logging, and a logger is not an answer to it.
public sealed class Prompt {
    readonly ILogger logger;

    public Prompt(ILogger logger) => this.logger = logger;

    public string? Ask() => Console.ReadLine();
}
