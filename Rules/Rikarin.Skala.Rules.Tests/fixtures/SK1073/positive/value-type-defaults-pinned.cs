// A struct's default value has no identity to observe, so `new Guid()`, `new TimeSpan()` and a fresh
// `CancellationToken` are their cached members exactly. Pinned by Probe (#425).
using System;
using System.Threading;

public static class Probe {
    public static string Run() {
        var id = new Guid();
        var span = new TimeSpan();
        var token = new CancellationToken();
        return id + "/" + span + "/" + token.CanBeCanceled + "/" + token.Equals(CancellationToken.None);
    }
}
