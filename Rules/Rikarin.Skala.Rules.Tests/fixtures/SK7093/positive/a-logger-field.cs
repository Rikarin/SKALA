using Microsoft.Extensions.Logging;
using System;

namespace Fixtures;

public sealed class Importer {
    readonly ILogger logger;

    public Importer(ILogger logger) => this.logger = logger;

    public void Import(string path) {
        Console.WriteLine($"importing {path}");
    }
}
