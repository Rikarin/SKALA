using System;

public sealed class Refusing {
    public static int Reject(string reason) {
        string Describe() => "rejected: " + reason;

        throw new InvalidOperationException(Describe());
    }
}
