using System.IO;

namespace Microsoft.Extensions.Logging;

public interface ILogger { }

// ⚠ Routing, not the finding. `Console.Error.WriteLine` binds to `TextWriter.WriteLine` too, so the
// rule reads the receiver rather than the method's containing type — a writer the caller chose is
// exactly what the rule is asking for.
public sealed class Report {
    readonly ILogger logger;

    public Report(ILogger logger) => this.logger = logger;

    public void Render(TextWriter output) {
        output.WriteLine("total: 12");
    }
}
