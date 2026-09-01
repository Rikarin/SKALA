using System;
using System.IO;

// ⚠ The half the rule declines to prove. A bare message beside a `throw;` is name-matching away
// from reporting every method in the tree called `Error`, so it is left alone deliberately.
public interface IAuditLog {
    void Error(string message);
}

public sealed class Ledger {
    readonly IAuditLog log;

    public Ledger(IAuditLog log) => this.log = log;

    public void Post(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException) {
            log.Error("the posting failed");
            throw;
        }
    }
}
