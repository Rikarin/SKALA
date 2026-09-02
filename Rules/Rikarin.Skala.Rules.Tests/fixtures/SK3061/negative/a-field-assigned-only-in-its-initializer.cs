public sealed class Tally {
    // The same argument as the constructor case, written the shorter way: the initializer runs once
    // per instance and before anybody can reach the lock, so the monitor is fixed for the object's
    // whole life. The second fixture the "effectively readonly" gate deserves.
    //
    // ⚠ It does not, on its own, defend the analyzer's `FieldDeclarationSyntax` exclusion, and
    // saying it did would be the kind of claim this repository asks to be checked. A declarator's
    // name is a token rather than an `IdentifierNameSyntax`, so a field's *own* initializer never
    // looks like a write to the walk in the first place. `a-static-gate-set-by-another-initializer`
    // is the fixture that actually reaches that branch.
    object gate = new object();

    int count;

    public void Bump() {
        lock (gate) {
            count++;
        }
    }
}
