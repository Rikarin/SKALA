using System;
using System.IO;

namespace Microsoft.Extensions.Logging;

public interface ILogger { }

public static class LoggerExtensions {
    public static void LogError(this ILogger logger, Exception error, string message) { }
}

public sealed class Importer {
    readonly ILogger logger;

    public Importer(ILogger logger) => this.logger = logger;

    public void Import(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException error) {
            logger.LogError(error, "the import failed");
            throw;
        }
    }
}
