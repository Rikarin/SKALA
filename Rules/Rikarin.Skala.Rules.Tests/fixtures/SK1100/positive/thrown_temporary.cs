using System;

public sealed class Guarding {
    public static void Reject(string reason) {
        var error = new InvalidOperationException(reason);
        throw error;
    }
}
