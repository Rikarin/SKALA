using System;

// Recording what the process should report is the repair the rule is asking for, not the finding.
public sealed class Runner {
    public void Run(bool ok) {
        if (!ok) {
            Environment.ExitCode = 1;
        }
    }
}
