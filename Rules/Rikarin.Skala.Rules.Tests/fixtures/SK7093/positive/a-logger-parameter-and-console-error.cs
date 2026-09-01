using System;

namespace Serilog;

public interface ILogger { }

public sealed class Exporter {
    public void Export(ILogger log, string path) {
        Console.Error.WriteLine($"the export of {path} failed");
    }
}
