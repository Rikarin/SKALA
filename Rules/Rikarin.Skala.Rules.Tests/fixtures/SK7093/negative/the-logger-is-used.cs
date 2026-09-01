namespace Microsoft.Extensions.Logging;

public interface ILogger {
    void LogInformation(string template, params object[] arguments);
}

public sealed class Importer {
    readonly ILogger logger;

    public Importer(ILogger logger) => this.logger = logger;

    public void Import(string path) {
        logger.LogInformation("importing {Path}", path);
    }
}
