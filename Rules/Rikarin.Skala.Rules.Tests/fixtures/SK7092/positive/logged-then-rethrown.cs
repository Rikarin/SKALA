using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace Fixtures;

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
