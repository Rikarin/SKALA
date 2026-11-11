using Microsoft.Extensions.Logging;
namespace Fixtures;

public sealed class Importer {
    readonly ILogger logger;

    public Importer(ILogger logger) => this.logger = logger;

    public void Import(string path) {
        logger.LogInformation("importing {Path}", path);
    }
}
