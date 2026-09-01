using System;

// `throw error;` sends on the same exception this layer just recorded — the same duplication, with
// the stack trace reset on top of it.
public interface IAuditLog {
    void Error(Exception error, string message);
}

public sealed class Ledger {
    readonly IAuditLog log;

    public Ledger(IAuditLog log) => this.log = log;

    public void Post(Action work) {
        try {
            work();
        } catch (InvalidOperationException error) {
            log.Error(error, "the posting failed");
            throw error;
        }
    }
}
