using System;

// Sonar's S3717 tracks every use of the type. This rule reports the member that compiles and does
// not work, so a construction handed to a helper is not it — nothing here fails when it runs.
public sealed class Assertions {
    public bool IsPending(Exception error) => error is NotImplementedException;

    public Exception Pending() {
        var expected = new NotImplementedException();
        return expected;
    }

    public void Swallow(Action work) {
        try {
            work();
        } catch (NotImplementedException) { }
    }
}
