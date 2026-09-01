using System;

// ⚠ The rule's stated position. Skipping cleanup is the whole point of `FailFast`: it is what an
// author writes when running `finally` blocks would be the more dangerous act, and it leaves a dump
// saying so. Reporting it would be reporting a decision rather than an accident.
public sealed class Ledger {
    public void Apply(int balance) {
        if (balance < 0) {
            Environment.FailFast("the ledger balance went negative; the in-memory state is corrupt");
        }
    }
}
